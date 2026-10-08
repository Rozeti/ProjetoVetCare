using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-003: cadastro, edição, inativação e exclusão de pacientes vinculados a um tutor.</summary>
    public class GerenciarPacientesUseCase
    {
        private readonly IPetRepository _pets;
        private readonly ITutorRepository _tutores;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IAlergiaRepository _alergias;
        private readonly IVacinaRepository _vacinas;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPacientesUseCase(
            IPetRepository pets,
            ITutorRepository tutores,
            IProntuarioRepository prontuarios,
            IAlergiaRepository alergias,
            IVacinaRepository vacinas,
            ITratamentoRepository tratamentos,
            IVeterinarioRepository veterinarios,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _pets = pets;
            _tutores = tutores;
            _prontuarios = prontuarios;
            _alergias = alergias;
            _vacinas = vacinas;
            _tratamentos = tratamentos;
            _veterinarios = veterinarios;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>
        /// Cada perfil enxerga um recorte: o tutor os próprios pets (HU-013, CA-1), o
        /// veterinário os pacientes sob sua responsabilidade, e administração e apoio a
        /// clínica inteira, com os filtros por veterinário e "sem responsável" à disposição.
        /// </summary>
        public async Task<Resultado<PaginaDe<PetDTO>>> Listar(
            Guid? tutorId,
            Guid? veterinarioId,
            bool apenasSemResponsavel,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            var filtro = new FiltroDePacientes
            {
                TutorId = tutorId,
                VeterinarioResponsavelId = veterinarioId,
                ApenasSemResponsavel = apenasSemResponsavel,
                Busca = busca,
                Ativo = ativo
            };

            if (_usuarioAtual.EhTutor)
            {
                if (_usuarioAtual.TutorId == null)
                {
                    return Resultado<PaginaDe<PetDTO>>.Ok(
                        PaginaDe<PetDTO>.Criar(Array.Empty<PetDTO>(), 1, parametros.Tamanho, 0));
                }

                filtro = new FiltroDePacientes { TutorId = _usuarioAtual.TutorId, Busca = busca, Ativo = ativo };
            }
            else if (_usuarioAtual.EhVeterinario)
            {
                if (_usuarioAtual.VeterinarioId == null)
                {
                    return Resultado<PaginaDe<PetDTO>>.Ok(
                        PaginaDe<PetDTO>.Criar(Array.Empty<PetDTO>(), 1, parametros.Tamanho, 0));
                }

                // O recorte do veterinário vem do token; o filtro da URL não o amplia.
                filtro = new FiltroDePacientes
                {
                    TutorId = tutorId,
                    VeterinarioResponsavelId = _usuarioAtual.VeterinarioId,
                    Busca = busca,
                    Ativo = ativo
                };
            }

            var pagina = await _pets.Listar(_usuarioAtual.ClinicaId, filtro, parametros);

            // Doses vencidas aparecem como alerta na lista, ao lado das alergias e comorbidades.
            var vencidas = await _vacinas.ContarVencidasPorPaciente(pagina.Itens.Select(p => p.Id));

            return Resultado<PaginaDe<PetDTO>>.Ok(pagina.Converter(pet =>
            {
                var dto = MapearParaDTO(pet);
                dto.VacinasVencidas = vencidas.GetValueOrDefault(pet.Id);
                return dto;
            }));
        }

        /// <summary>
        /// Lista enxuta para os seletores (agendamento, mensagens): todos os pacientes ativos
        /// que quem consulta enxerga, sem o teto de página da listagem.
        /// </summary>
        public async Task<Resultado<List<PetSelecaoDTO>>> ListarParaSelecao()
        {
            var filtro = new FiltroDePacientes();

            if (_usuarioAtual.EhTutor)
            {
                filtro = new FiltroDePacientes { TutorId = _usuarioAtual.TutorId ?? Guid.Empty };
            }
            else if (_usuarioAtual.EhVeterinario)
            {
                filtro = new FiltroDePacientes { VeterinarioResponsavelId = _usuarioAtual.VeterinarioId ?? Guid.Empty };
            }

            var pets = await _pets.ListarParaSelecao(_usuarioAtual.ClinicaId, filtro);

            return Resultado<List<PetSelecaoDTO>>.Ok(pets.Select(p => new PetSelecaoDTO
            {
                Id = p.Id,
                Nome = p.Nome,
                Especie = p.Especie,
                TutorId = p.TutorId,
                TutorUsuarioId = p.Tutor?.UsuarioId ?? Guid.Empty,
                NomeTutor = p.Tutor?.Usuario?.Nome ?? string.Empty,
                VeterinarioResponsavelId = p.VeterinarioResponsavelId
            }).ToList());
        }

        public async Task<Resultado<PetDTO>> ObterPorId(Guid id)
        {
            var pet = await _pets.ObterPorIdComTutor(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<PetDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var dto = MapearParaDTO(pet);

            var alertas = await _alergias.ObterPorPaciente(id, apenasAtivas: true);
            dto.AlertasClinicos = alertas.Select(GerenciarAlergiasUseCase.MapearParaDTO).ToList();

            var vencidas = await _vacinas.ContarVencidasPorPaciente(new[] { id });
            dto.VacinasVencidas = vencidas.GetValueOrDefault(id);

            return Resultado<PetDTO>.Ok(dto);
        }

        /// <summary>
        /// HU-003 estendida: o tutor cadastra um animal recém-adquirido sem depender de um
        /// atendimento presencial. O vínculo da RN-001 vem do próprio token, nunca do corpo
        /// da requisição — é isso que impede alguém de registrar um pet no nome de outro.
        /// </summary>
        public async Task<Resultado<PetDTO>> CadastrarComoTutor(CriarPetDoTutorDTO dto)
        {
            if (!_usuarioAtual.EhTutor)
            {
                return Resultado<PetDTO>.NaoAutorizado(
                    "Este cadastro é exclusivo do tutor responsável pelo animal.");
            }

            if (_usuarioAtual.TutorId == null)
            {
                return Resultado<PetDTO>.NaoEncontrado(
                    "Não encontramos o seu cadastro de tutor. Procure a clínica para regularizá-lo.");
            }

            return await Cadastrar(
                new CriarPetDTO
                {
                    TutorId = _usuarioAtual.TutorId.Value,
                    Nome = dto.Nome,
                    Especie = dto.Especie,
                    Raca = dto.Raca,
                    Sexo = dto.Sexo,
                    Pelagem = dto.Pelagem,
                    Microchip = dto.Microchip,
                    Castrado = dto.Castrado,
                    PesoAtualKg = dto.PesoAtualKg,
                    DataNascimento = dto.DataNascimento
                },
                cadastradoPeloTutor: true);
        }

        public async Task<Resultado<PetDTO>> Cadastrar(CriarPetDTO dto, bool cadastradoPeloTutor = false)
        {
            // HU-003, CA-2: sem tutor informado o cadastro é impedido (RN-001).
            if (dto.TutorId == Guid.Empty)
            {
                return Resultado<PetDTO>.Invalido("O paciente precisa estar vinculado a um tutor responsável.");
            }

            var tutor = await _tutores.ObterPorId(dto.TutorId);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.Invalido("O tutor informado não foi encontrado nesta clínica.");
            }

            // O veterinário cadastra pacientes para os tutores que já enxerga (ou que ainda não têm pet).
            if (!AcessoAoTutor.Permitido(_usuarioAtual, tutor))
            {
                return Resultado<PetDTO>.NaoAutorizado("Você não tem acesso ao tutor informado.");
            }

            if (dto.DataNascimento.Date > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<PetDTO>.Invalido("A data de nascimento não pode ser futura.");
            }

            var microchip = dto.Microchip.Trim();

            if (microchip.Length > 0 && await _pets.MicrochipEmUso(_usuarioAtual.ClinicaId, microchip))
            {
                return Resultado<PetDTO>.Conflito("Já existe um paciente cadastrado com este microchip.");
            }

            var (responsavel, erroDoResponsavel) = await ResolverResponsavel(dto.VeterinarioResponsavelId, cadastradoPeloTutor);

            if (erroDoResponsavel != null)
            {
                return Resultado<PetDTO>.Invalido(erroDoResponsavel);
            }

            var pet = new Pet
            {
                ClinicaId = _usuarioAtual.ClinicaId,
                TutorId = dto.TutorId,
                VeterinarioResponsavelId = responsavel?.Id,
                Nome = dto.Nome.Trim(),
                Especie = dto.Especie.Trim(),
                Raca = dto.Raca.Trim(),
                Sexo = dto.Sexo.Trim(),
                Pelagem = dto.Pelagem.Trim(),
                Microchip = microchip,
                Castrado = dto.Castrado,
                PesoAtualKg = dto.PesoAtualKg,
                DataNascimento = DateTime.SpecifyKind(dto.DataNascimento.Date, DateTimeKind.Utc)
            };

            await _pets.Adicionar(pet);
            await _pets.SalvarAlteracoes();

            // O prontuário nasce junto com o paciente, garantindo a linha do tempo desde o início.
            await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

            pet.Tutor = tutor;
            pet.VeterinarioResponsavel = responsavel;

            // A origem fica registrada: a equipe precisa saber que o cadastro veio de fora
            // do balcão para conferir os dados no primeiro atendimento.
            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao,
                "Paciente",
                pet.Id,
                cadastradoPeloTutor
                    ? $"Cadastro de {pet.Nome} feito pelo tutor responsável"
                    : $"Cadastro de {pet.Nome}");

            return Resultado<PetDTO>.Ok(
                MapearParaDTO(pet),
                cadastradoPeloTutor
                    ? $"{pet.Nome} foi cadastrado e já aparece para a equipe da clínica."
                    : "Paciente cadastrado com sucesso.");
        }

        public async Task<Resultado<PetDTO>> Atualizar(Guid id, AtualizarPetDTO dto)
        {
            var pet = await _pets.ObterPorIdComTutor(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<PetDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var tutor = await _tutores.ObterPorId(dto.TutorId);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.Invalido("O tutor informado não foi encontrado nesta clínica.");
            }

            // Trocar o tutor só para alguém que quem edita já enxerga: o recorte dos tutores não se amplia por aqui.
            if (pet.TutorId != dto.TutorId && !AcessoAoTutor.Permitido(_usuarioAtual, tutor))
            {
                return Resultado<PetDTO>.NaoAutorizado("Você não tem acesso ao tutor informado.");
            }

            if (dto.DataNascimento == default)
            {
                return Resultado<PetDTO>.Invalido("Informe a data de nascimento do paciente.");
            }

            if (dto.DataNascimento.Date > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<PetDTO>.Invalido("A data de nascimento não pode ser futura.");
            }

            if (dto.DataObito.HasValue && dto.DataObito.Value.Date < dto.DataNascimento.Date)
            {
                return Resultado<PetDTO>.Invalido("A data de óbito não pode ser anterior à data de nascimento.");
            }

            // O óbito inativa o paciente e encerra os tratamentos: uma data futura faria isso antes da hora.
            if (dto.DataObito.HasValue && dto.DataObito.Value.Date > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<PetDTO>.Invalido("A data de óbito não pode ser futura.");
            }

            var microchip = dto.Microchip.Trim();

            if (microchip.Length > 0 && await _pets.MicrochipEmUso(_usuarioAtual.ClinicaId, microchip, id))
            {
                return Resultado<PetDTO>.Conflito("Já existe outro paciente cadastrado com este microchip.");
            }

            // HU-003, CA-3 e RN-001: alterar o cadastro (inclusive o tutor) preserva o histórico clínico,
            // pois avaliações e atendimentos permanecem ligados ao mesmo prontuário do paciente.
            pet.Nome = dto.Nome.Trim();
            pet.Especie = dto.Especie.Trim();
            pet.Raca = dto.Raca.Trim();
            pet.Sexo = dto.Sexo.Trim();
            pet.Pelagem = dto.Pelagem.Trim();
            pet.Microchip = microchip;
            pet.Castrado = dto.Castrado;
            pet.PesoAtualKg = dto.PesoAtualKg;
            pet.DataNascimento = DateTime.SpecifyKind(dto.DataNascimento.Date, DateTimeKind.Utc);
            pet.TutorId = dto.TutorId;
            pet.Tutor = tutor;

            var registrouObito = dto.DataObito.HasValue && pet.DataObito?.Date != dto.DataObito.Value.Date;
            var desfezObito = !dto.DataObito.HasValue && pet.DataObito.HasValue;

            pet.DataObito = dto.DataObito.HasValue
                ? DateTime.SpecifyKind(dto.DataObito.Value.Date, DateTimeKind.Utc)
                : null;

            // O óbito encerra o acompanhamento: o paciente sai das listas ativas e os
            // tratamentos em aberto deixam de aceitar novos agendamentos.
            if (registrouObito)
            {
                pet.Ativo = false;
                await EncerrarTratamentosEmAberto(pet.Id);
            }

            // Um óbito registrado por engano pode ser desfeito: o paciente volta a ficar ativo
            // (os tratamentos interrompidos são retomados manualmente, se for o caso).
            if (desfezObito)
            {
                pet.Ativo = true;
            }

            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Paciente", pet.Id,
                registrouObito ? $"Edição de {pet.Nome} com registro de óbito" : $"Edição de {pet.Nome}");

            return Resultado<PetDTO>.Ok(MapearParaDTO(pet), "Paciente atualizado com sucesso.");
        }

        public async Task<Resultado> AlterarStatus(Guid id, bool ativo)
        {
            var pet = await _pets.ObterPorId(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            pet.Ativo = ativo;

            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Inativacao, "Paciente", pet.Id,
                ativo ? $"Reativação de {pet.Nome}" : $"Inativação de {pet.Nome}");

            return Resultado.Ok(ativo ? "Paciente reativado com sucesso." : "Paciente inativado com sucesso.");
        }

        /// <summary>
        /// HU-003, CA-4: a exclusão é bloqueada quando existe histórico clínico; nesse caso o
        /// sistema oferece apenas a inativação, preservando a integridade do prontuário (RN-004).
        /// Sem histórico, o cadastro é removido de fato.
        /// </summary>
        public async Task<Resultado> Excluir(Guid id)
        {
            var pet = await _pets.ObterPorId(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (await _pets.PossuiRegistrosClinicos(id))
            {
                return Resultado.Conflito(
                    "Este paciente possui prontuário com registros e não pode ser excluído. Utilize a inativação.");
            }

            await _pets.Remover(pet);
            await _pets.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Exclusao, "Paciente", pet.Id, $"Exclusão de {pet.Nome} (sem histórico clínico)");

            return Resultado.Ok("Paciente excluído com sucesso.");
        }

        /// <summary>
        /// Quem cadastra sendo veterinário assume o paciente; administração e apoio escolhem
        /// o profissional (ou deixam para depois). O cadastro feito pelo tutor nasce sem
        /// responsável: a clínica designa alguém quando o animal chega para o primeiro atendimento.
        /// </summary>
        private async Task<(Veterinario? Responsavel, string? Erro)> ResolverResponsavel(
            Guid? veterinarioInformado,
            bool cadastradoPeloTutor)
        {
            if (cadastradoPeloTutor)
            {
                return (null, null);
            }

            var veterinarioId = _usuarioAtual.EhVeterinario ? _usuarioAtual.VeterinarioId : veterinarioInformado;

            if (_usuarioAtual.EhVeterinario && veterinarioId == null)
            {
                return (null, "Cadastro de veterinário não encontrado para este usuário.");
            }

            if (veterinarioId == null || veterinarioId == Guid.Empty)
            {
                return (null, null);
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId.Value);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return (null, "O veterinário responsável informado não foi encontrado nesta clínica.");
            }

            return (veterinario, null);
        }

        private async Task EncerrarTratamentosEmAberto(Guid pacienteId)
        {
            var emAberto = (await _tratamentos.ObterPorPaciente(pacienteId))
                .Where(t => t.Status == StatusTratamento.EmAndamento)
                .ToList();

            foreach (var tratamento in emAberto)
            {
                var atual = await _tratamentos.ObterPorId(tratamento.Id);

                if (atual == null)
                {
                    continue;
                }

                atual.Status = StatusTratamento.Interrompido;
                atual.DataFim ??= DateTime.UtcNow;

                _tratamentos.Atualizar(atual);
            }

            if (emAberto.Count > 0)
            {
                await _tratamentos.SalvarAlteracoes();
            }
        }

        public static PetDTO MapearParaDTO(Pet pet)
        {
            return new PetDTO
            {
                Id = pet.Id,
                Nome = pet.Nome,
                Especie = pet.Especie,
                Raca = pet.Raca,
                Sexo = pet.Sexo,
                Pelagem = pet.Pelagem,
                Microchip = pet.Microchip,
                Castrado = pet.Castrado,
                DataNascimento = pet.DataNascimento,
                IdadeAnos = CalcularIdade(pet.DataNascimento),
                IdadeDescritiva = DescreverIdade(pet.DataNascimento),
                PesoAtualKg = pet.PesoAtualKg,
                TutorId = pet.TutorId,
                NomeTutor = pet.Tutor?.Usuario?.Nome ?? string.Empty,
                TelefoneTutor = pet.Tutor?.Telefone ?? string.Empty,
                VeterinarioResponsavelId = pet.VeterinarioResponsavelId,
                NomeVeterinarioResponsavel = pet.VeterinarioResponsavel?.Usuario?.Nome ?? string.Empty,
                Ativo = pet.Ativo,
                DataObito = pet.DataObito,
                AlertasClinicos = pet.AlergiasCondicoes
                    .Where(a => a.Ativa)
                    .Select(GerenciarAlergiasUseCase.MapearParaDTO)
                    .ToList()
            };
        }

        public static int CalcularIdade(DateTime dataNascimento)
        {
            var hoje = RelogioDaClinica.Padrao.Hoje;
            var idade = hoje.Year - dataNascimento.Year;

            if (dataNascimento.Date > hoje.AddYears(-idade))
            {
                idade--;
            }

            return Math.Max(0, idade);
        }

        /// <summary>
        /// Em filhotes a idade em anos arredonda para zero e não diz nada ao clínico;
        /// por isso abaixo de um ano a descrição passa a ser em meses.
        /// </summary>
        public static string DescreverIdade(DateTime dataNascimento)
        {
            var hoje = RelogioDaClinica.Padrao.Hoje;
            var anos = CalcularIdade(dataNascimento);

            if (anos >= 1)
            {
                return $"{anos} {(anos == 1 ? "ano" : "anos")}";
            }

            var meses = ((hoje.Year - dataNascimento.Year) * 12) + hoje.Month - dataNascimento.Month;

            if (hoje.Day < dataNascimento.Day)
            {
                meses--;
            }

            meses = Math.Max(0, meses);

            if (meses >= 1)
            {
                return $"{meses} {(meses == 1 ? "mês" : "meses")}";
            }

            var dias = Math.Max(0, (hoje - dataNascimento.Date).Days);

            return $"{dias} {(dias == 1 ? "dia" : "dias")}";
        }
    }
}
