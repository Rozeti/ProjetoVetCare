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
        /// O veterinário vê os tutores dos pacientes sob sua responsabilidade (e os que ainda
        /// não têm paciente); administração e apoio veem todos. O recorte vem do token.
        /// </summary>
        public async Task<Resultado<PaginaDe<TutorDTO>>> Listar(string? busca, ParametrosPagina parametros)
        {
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId == null)
            {
                return Resultado<PaginaDe<TutorDTO>>.Ok(
                    PaginaDe<TutorDTO>.Criar(Array.Empty<TutorDTO>(), 1, parametros.Tamanho, 0));
            }

            var pagina = await _tutores.Listar(_usuarioAtual.ClinicaId, busca, parametros, RecorteDoVeterinario);
            return Resultado<PaginaDe<TutorDTO>>.Ok(pagina.Converter(MapearParaDTO));
        }

        /// <summary>Lista completa usada pelos seletores de tutor nos formulários, no mesmo recorte da listagem.</summary>
        public async Task<Resultado<List<TutorDTO>>> ListarParaSelecao()
        {
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId == null)
            {
                return Resultado<List<TutorDTO>>.Ok(new List<TutorDTO>());
            }

            var tutores = await _tutores.ListarTodos(_usuarioAtual.ClinicaId, RecorteDoVeterinario);
            return Resultado<List<TutorDTO>>.Ok(tutores.Select(MapearParaDTO).ToList());
        }

        public async Task<Resultado<TutorDTO>> ObterPorId(Guid id)
        {
            var tutor = await _tutores.ObterPorId(id);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            // HU-013, CA-1: o tutor enxerga apenas o próprio cadastro; o veterinário, os
            // tutores dos seus pacientes. Para os demais o cadastro simplesmente não existe.
            if (!AcessoAoTutor.Permitido(_usuarioAtual, tutor))
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor));
        }

        private Guid? RecorteDoVeterinario => _usuarioAtual.EhVeterinario ? _usuarioAtual.VeterinarioId : null;

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

                // RN-007: unicidade de credenciais.
                if (await _usuarios.EmailExiste(dto.Email))
                {
                    return Resultado<TutorDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
                }

                if (senhaDefinidaPelaClinica && PasswordHasher.ValidarForca(dto.Senha) is { } erroDeSenha)
                {
                    return Resultado<TutorDTO>.Invalido(erroDeSenha);
                }

                usuario = new Usuario
                {
                    ClinicaId = _usuarioAtual.ClinicaId,
                    Nome = dto.Nome.Trim(),
                    Email = dto.Email.Trim().ToLowerInvariant(),
                    SenhaHash = _hasher.Gerar(senhaDefinidaPelaClinica ? dto.Senha! : _hasher.GerarSenhaProvisoria()),
                    Perfil = Perfis.Tutor
                };

                // Gravado junto com o tutor, mais abaixo, para que um usuário nunca
                // fique sem o cadastro correspondente.
                await _usuarios.Adicionar(usuario);
                usuarioNovo = true;
            }

            var tutor = new Tutor
            {
                UsuarioId = usuario.Id,
                Telefone = dto.Telefone.Trim(),
                Endereco = dto.Endereco.Trim(),
                Cpf = dto.Cpf.Trim()
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
                    : "Este tutor não tem pacientes sob sua responsabilidade.");
            }

            tutor.Telefone = dto.Telefone?.Trim() ?? tutor.Telefone;
            tutor.Endereco = dto.Endereco?.Trim() ?? tutor.Endereco;
            tutor.Cpf = dto.Cpf?.Trim() ?? tutor.Cpf;

            _tutores.Atualizar(tutor);
            await _tutores.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Tutor", tutor.Id,
                _usuarioAtual.EhTutor ? "Contato atualizado pelo próprio tutor" : $"Edição do tutor {tutor.Usuario?.Nome}");

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor), "Tutor atualizado com sucesso.");
        }

        private TutorDTO MapearParaDTO(Tutor tutor)
        {
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
                // O veterinário conta só os pacientes que enxerga.
                QuantidadePets = AcessoAoTutor.ContarPacientesVisiveis(_usuarioAtual, tutor)
            };
        }
    }
}
