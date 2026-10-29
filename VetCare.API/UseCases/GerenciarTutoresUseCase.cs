using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    public class GerenciarTutoresUseCase
    {
        private readonly ITutorRepository _tutores;
        private readonly IUsuarioRepository _usuarios;
        private readonly PasswordHasher _hasher;
        private readonly ContasService _contas;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarTutoresUseCase(
            ITutorRepository tutores,
            IUsuarioRepository usuarios,
            PasswordHasher hasher,
            ContasService contas,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _tutores = tutores;
            _usuarios = usuarios;
            _hasher = hasher;
            _contas = contas;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>
        /// A equipe clínica inteira (administração, apoio e todos os veterinários) vê todos os
        /// tutores da clínica, assim como vê todos os pacientes.
        /// </summary>
        public async Task<Resultado<PaginaDe<TutorDTO>>> Listar(string? busca, ParametrosPagina parametros)
        {
            var pagina = await _tutores.Listar(_usuarioAtual.ClinicaId, busca, parametros);
            return Resultado<PaginaDe<TutorDTO>>.Ok(pagina.Converter(MapearParaDTO));
        }

        /// <summary>Lista completa usada pelos seletores de tutor nos formulários.</summary>
        public async Task<Resultado<List<TutorDTO>>> ListarParaSelecao()
        {
            var tutores = await _tutores.ListarTodos(_usuarioAtual.ClinicaId);
            return Resultado<List<TutorDTO>>.Ok(tutores.Select(MapearParaDTO).ToList());
        }

        public async Task<Resultado<TutorDTO>> ObterPorId(Guid id)
        {
            var tutor = await _tutores.ObterPorId(id);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            // HU-013, CA-1: o tutor enxerga apenas o próprio cadastro. Para ele os demais
            // cadastros simplesmente não existem.
            if (!AcessoAoTutor.Permitido(_usuarioAtual, tutor))
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor));
        }

        /// <summary>
        /// Cadastra o tutor. Quando UsuarioId não é informado, cria também o usuário de
        /// acesso — é o caminho usado pela recepção ao registrar um tutor novo. O tutor
        /// recebe um e-mail de boas-vindas; sem senha informada, ele mesmo cria a sua.
        /// </summary>
        public async Task<Resultado<TutorDTO>> Cadastrar(CriarTutorDTO dto)
        {
            Usuario usuario;
            var usuarioNovo = false;
            var senhaDefinidaPelaClinica = !string.IsNullOrWhiteSpace(dto.Senha);

            // Contato: o telefone é obrigatório (é por ele que a clínica fala com o tutor); CPF e
            // endereço são opcionais, mas, quando vêm, precisam estar corretos.
            var (telefone, erroDeTelefone) = ValidacaoDeCadastro.Telefone(dto.Telefone, obrigatorio: true, "o telefone do tutor");

            if (erroDeTelefone != null)
            {
                return Resultado<TutorDTO>.Invalido(erroDeTelefone);
            }

            var (cpf, erroDeCpf) = ValidacaoDeCadastro.Cpf(dto.Cpf);

            if (erroDeCpf != null)
            {
                return Resultado<TutorDTO>.Invalido(erroDeCpf);
            }

            if (ValidacaoDeCadastro.Endereco(dto.Endereco) is { } erroDeEndereco)
            {
                return Resultado<TutorDTO>.Invalido(erroDeEndereco);
            }

            if (dto.UsuarioId.HasValue && dto.UsuarioId.Value != Guid.Empty)
            {
                var existente = await _usuarios.ObterPorId(dto.UsuarioId.Value);

                if (existente == null || existente.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<TutorDTO>.NaoEncontrado("Usuário informado não encontrado.");
                }

                if (await _tutores.ObterPorUsuarioId(existente.Id) != null)
                {
                    return Resultado<TutorDTO>.Conflito("Este usuário já possui um cadastro de tutor.");
                }

                usuario = existente;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Email))
                {
                    return Resultado<TutorDTO>.Invalido(
                        "Informe nome e e-mail para criar o acesso do tutor, ou envie o UsuarioId de um usuário existente.");
                }

                if (ValidacaoDeCadastro.Nome(dto.Nome) is { } erroDeNome)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeNome);
                }

                if (ValidacaoDeCadastro.Email(dto.Email) is { } erroDeEmail)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeEmail);
                }

                if (senhaDefinidaPelaClinica && PasswordHasher.ValidarForca(dto.Senha) is { } erroDeSenha)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeSenha);
                }

                // RN-007: unicidade de credenciais.
                if (await _usuarios.EmailExiste(dto.Email.Trim()))
                {
                    return Resultado<TutorDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
                }

                usuario = new Usuario
                {
                    ClinicaId = _usuarioAtual.ClinicaId,
                    Nome = ValidacaoDeCadastro.Compactar(dto.Nome),
                    Email = dto.Email.Trim().ToLowerInvariant(),
                    SenhaHash = _hasher.Gerar(senhaDefinidaPelaClinica ? dto.Senha! : _hasher.GerarSenhaProvisoria()),
                    Perfil = Perfis.Tutor
                };

                // Gravado junto com o tutor, mais abaixo, para que um usuário nunca
                // fique sem o cadastro correspondente.
                await _usuarios.Adicionar(usuario);
                usuarioNovo = true;
            }

            if (!string.IsNullOrEmpty(cpf) && await _tutores.CpfEmUso(_usuarioAtual.ClinicaId, cpf))
            {
                return Resultado<TutorDTO>.Conflito("Já existe um tutor cadastrado com este CPF.");
            }

            var tutor = new Tutor
            {
                UsuarioId = usuario.Id,
                Telefone = telefone!,
                Endereco = ValidacaoDeCadastro.Compactar(dto.Endereco),
                Cpf = cpf!
            };

            await _tutores.Adicionar(tutor);
            await _tutores.SalvarAlteracoes();

            tutor.Usuario = usuario;

            if (usuarioNovo)
            {
                await _contas.EnviarBoasVindas(usuario, senhaDefinidaPelaClinica);
            }

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Tutor", tutor.Id,
                $"Cadastro de {usuario.Nome}{(usuarioNovo ? " com conta nova" : " para conta existente")}");

            return Resultado<TutorDTO>.Ok(
                MapearParaDTO(tutor),
                usuarioNovo && !senhaDefinidaPelaClinica
                    ? "Tutor cadastrado. Ele recebeu por e-mail o link para criar a própria senha."
                    : "Tutor cadastrado com sucesso.");
        }

        /// <summary>Campos omitidos (nulos) mantêm o valor atual; o próprio tutor pode manter o contato em dia.</summary>
        public async Task<Resultado<TutorDTO>> Atualizar(Guid id, AtualizarTutorDTO dto)
        {
            var tutor = await _tutores.ObterPorId(id);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            if (!AcessoAoTutor.Permitido(_usuarioAtual, tutor))
            {
                return Resultado<TutorDTO>.NaoAutorizado(_usuarioAtual.EhTutor
                    ? "Você só pode alterar o seu próprio cadastro."
                    : "Você não tem acesso a este tutor.");
            }

            // Nome e e-mail são o acesso da pessoa: só a equipe clínica os altera.
            if (_usuarioAtual.EhTutor && (dto.Nome != null || dto.Email != null))
            {
                return Resultado<TutorDTO>.NaoAutorizado("Nome e e-mail são alterados pela clínica. Fale com a recepção.");
            }

            var usuario = tutor.Usuario!;

            if (dto.Nome != null)
            {
                var nome = ValidacaoDeCadastro.Compactar(dto.Nome);

                if (nome != usuario.Nome && ValidacaoDeCadastro.Nome(nome) is { } erroDeNome)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeNome);
                }

                usuario.Nome = nome;
            }

            if (dto.Email != null)
            {
                if (ValidacaoDeCadastro.Email(dto.Email) is { } erroDeEmail)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeEmail);
                }

                if (await _usuarios.EmailExiste(dto.Email.Trim(), usuario.Id))
                {
                    return Resultado<TutorDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
                }

                usuario.Email = dto.Email.Trim().ToLowerInvariant();
            }

            // O telefone continua obrigatório: enviado em branco, é recusado em vez de apagado.
            if (dto.Telefone != null)
            {
                var (telefone, erroDeTelefone) = ValidacaoDeCadastro.Telefone(dto.Telefone, obrigatorio: true, "o telefone do tutor");

                if (erroDeTelefone != null)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeTelefone);
                }

                tutor.Telefone = telefone!;
            }

            if (dto.Cpf != null)
            {
                var (cpf, erroDeCpf) = ValidacaoDeCadastro.Cpf(dto.Cpf);

                if (erroDeCpf != null)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeCpf);
                }

                if (!string.IsNullOrEmpty(cpf) && await _tutores.CpfEmUso(_usuarioAtual.ClinicaId, cpf, tutor.Id))
                {
                    return Resultado<TutorDTO>.Conflito("Já existe outro tutor cadastrado com este CPF.");
                }

                tutor.Cpf = cpf!;
            }

            if (dto.Endereco != null)
            {
                if (ValidacaoDeCadastro.Endereco(dto.Endereco) is { } erroDeEndereco)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeEndereco);
                }

                tutor.Endereco = ValidacaoDeCadastro.Compactar(dto.Endereco);
            }

            _usuarios.Atualizar(usuario);
            _tutores.Atualizar(tutor);
            await _tutores.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Tutor", tutor.Id,
                _usuarioAtual.EhTutor ? "Contato atualizado pelo próprio tutor" : $"Edição do tutor {tutor.Usuario?.Nome}");

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor), "Tutor atualizado com sucesso.");
        }

        private TutorDTO MapearParaDTO(Tutor tutor)
        {
            var visiveis = AcessoAoTutor.PacientesVisiveis(tutor);

            return new TutorDTO
            {
                Id = tutor.Id,
                UsuarioId = tutor.UsuarioId,
                Nome = tutor.Usuario?.Nome ?? string.Empty,
                Email = tutor.Usuario?.Email ?? string.Empty,
                Telefone = tutor.Telefone,
                Endereco = tutor.Endereco,
                Cpf = tutor.Cpf,
                Ativo = tutor.Usuario?.Ativo ?? false,
                QuantidadePets = visiveis.Count,
                Pets = visiveis
                    .OrderBy(p => p.Nome)
                    .Select(p => new PetDoTutorDTO { Id = p.Id, Nome = p.Nome, Especie = p.Especie, Ativo = p.Ativo })
                    .ToList()
            };
        }
    }
}
