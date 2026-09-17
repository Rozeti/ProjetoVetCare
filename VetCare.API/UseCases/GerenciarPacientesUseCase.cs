using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-003: cadastro, edição e inativação de pacientes vinculados a um tutor.</summary>
    public class GerenciarPacientesUseCase
    {
        private readonly IPetRepository _pets;
        private readonly ITutorRepository _tutores;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IAlergiaRepository _alergias;
        private readonly IVacinaRepository _vacinas;
        private readonly ITratamentoRepository _tratamentos;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPacientesUseCase(
            IPetRepository pets,
            ITutorRepository tutores,
            IProntuarioRepository prontuarios,
            IAlergiaRepository alergias,
            IVacinaRepository vacinas,
            ITratamentoRepository tratamentos,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _pets = pets;
            _tutores = tutores;
            _prontuarios = prontuarios;
            _alergias = alergias;
            _vacinas = vacinas;
            _tratamentos = tratamentos;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<PaginaDe<PetDTO>>> Listar(
            Guid? tutorId,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            // HU-013, CA-1: o tutor só enxerga os pets sob sua responsabilidade.
            if (_usuarioAtual.EhTutor)
            {
                tutorId = _usuarioAtual.TutorId;

                if (tutorId == null)
                {
                    return Resultado<PaginaDe<PetDTO>>.Ok(
                        PaginaDe<PetDTO>.Criar(Array.Empty<PetDTO>(), 1, parametros.Tamanho, 0));
                }
            }

            var pagina = await _pets.Listar(_usuarioAtual.ClinicaId, tutorId, busca, ativo, parametros);

            return Resultado<PaginaDe<PetDTO>>.Ok(pagina.Converter(MapearParaDTO));
        }

        public async Task<Resultado<PetDTO>> ObterPorId(Guid id)
        {
            var pet = await _pets.ObterPorIdComTutor(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<PetDTO>.NaoAutorizado("Você não tem acesso a este paciente.");
            }

            var dto = MapearParaDTO(pet);

            var alertas = await _alergias.ObterPorPaciente(id, apenasAtivas: true);
            dto.AlertasClinicos = alertas.Select(GerenciarAlergiasUseCase.MapearParaDTO).ToList();

            var vacinas = await _vacinas.ObterPorPaciente(id);
            dto.VacinasVencidas = vacinas.Count(v => v.ProximaDose.HasValue && v.ProximaDose < DateTime.UtcNow.Date);

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

            if (dto.DataNascimento.Date > DateTime.UtcNow.Date)
            {
                return Resultado<PetDTO>.Invalido("A data de nascimento não pode ser futura.");
            }

            var microchip = dto.Microchip.Trim();

            if (microchip.Length > 0 && await _pets.MicrochipEmUso(_usuarioAtual.ClinicaId, microchip))
            {
                return Resultado<PetDTO>.Conflito("Já existe um paciente cadastrado com este microchip.");
            }

            var pet = new Pet
            {
                ClinicaId = _usuarioAtual.ClinicaId,
                TutorId = dto.TutorId,
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
            var pet = await _pets.ObterPorId(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            var tutor = await _tutores.ObterPorId(dto.TutorId);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PetDTO>.Invalido("O tutor informado não foi encontrado nesta clínica.");
            }

            if (dto.DataNascimento.Date > DateTime.UtcNow.Date)
            {
                return Resultado<PetDTO>.Invalido("A data de nascimento não pode ser futura.");
            }

            if (dto.DataObito.HasValue && dto.DataObito.Value.Date < dto.DataNascimento.Date)
            {
                return Resultado<PetDTO>.Invalido("A data de óbito não pode ser anterior à data de nascimento.");
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

            var registrouObito = dto.DataObito.HasValue && pet.DataObito != dto.DataObito;

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

            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();

            pet.Tutor = tutor;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Paciente", pet.Id, $"Edição de {pet.Nome}");

            return Resultado<PetDTO>.Ok(MapearParaDTO(pet), "Paciente atualizado com sucesso.");
        }

        public async Task<Resultado> AlterarStatus(Guid id, bool ativo)
        {
            var pet = await _pets.ObterPorId(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Paciente não encontrado.");
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
        /// HU-003, CA-4: a exclusão é bloqueada quando existe histórico clínico; nesse caso
        /// o sistema oferece apenas a inativação, preservando a integridade do prontuário (RN-004).
        /// </summary>
        public async Task<Resultado> Excluir(Guid id)
        {
            var pet = await _pets.ObterPorId(id);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Paciente não encontrado.");
            }

            if (await _pets.PossuiRegistrosClinicos(id))
            {
                return Resultado.Conflito(
                    "Este paciente possui prontuário com registros e não pode ser excluído. Utilize a inativação.");
            }

            pet.Ativo = false;

            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();

            return Resultado.Ok("Paciente inativado com sucesso.");
        }

        private async Task EncerrarTratamentosEmAberto(Guid pacienteId)
        {
            var emAberto = (await _tratamentos.ObterPorPaciente(pacienteId))
                .Where(t => t.Status == "Em Andamento")
                .ToList();

            foreach (var tratamento in emAberto)
            {
                var atual = await _tratamentos.ObterPorId(tratamento.Id);

                if (atual == null)
                {
                    continue;
                }

                atual.Status = "Interrompido";
                atual.DataFim ??= DateTime.UtcNow;

                _tratamentos.Atualizar(atual);
            }

            if (emAberto.Count > 0)
            {
                await _tratamentos.SalvarAlteracoes();
            }
        }

        private static PetDTO MapearParaDTO(Pet pet)
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
            var hoje = DateTime.UtcNow.Date;
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
            var hoje = DateTime.UtcNow.Date;
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
