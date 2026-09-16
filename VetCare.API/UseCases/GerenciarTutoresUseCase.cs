using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    public class GerenciarTutoresUseCase
    {
        private readonly ITutorRepository _tutores;
        private readonly IUsuarioRepository _usuarios;
        private readonly PasswordHasher _hasher;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarTutoresUseCase(
            ITutorRepository tutores,
            IUsuarioRepository usuarios,
            PasswordHasher hasher,
            UsuarioAtual usuarioAtual)
        {
            _tutores = tutores;
            _usuarios = usuarios;
            _hasher = hasher;
            _usuarioAtual = usuarioAtual;
        }

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

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor));
        }

        /// <summary>
        /// Cadastra o tutor. Quando UsuarioId não é informado, cria também o usuário de
        /// acesso — é o caminho usado pela recepção ao registrar um tutor novo.
        /// </summary>
        public async Task<Resultado<TutorDTO>> Cadastrar(CriarTutorDTO dto)
        {
            Usuario usuario;

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

                var senha = string.IsNullOrWhiteSpace(dto.Senha) ? _hasher.GerarSenhaProvisoria() : dto.Senha;

                usuario = new Usuario
                {
                    ClinicaId = _usuarioAtual.ClinicaId,
                    Nome = dto.Nome.Trim(),
                    Email = dto.Email.Trim().ToLowerInvariant(),
                    SenhaHash = _hasher.Gerar(senha),
                    Perfil = Perfis.Tutor
                };

                // Gravado junto com o tutor, mais abaixo, para que um usuário nunca
                // fique sem o cadastro correspondente.
                await _usuarios.Adicionar(usuario);
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

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor), "Tutor cadastrado com sucesso.");
        }

        public async Task<Resultado<TutorDTO>> Atualizar(Guid id, AtualizarTutorDTO dto)
        {
            var tutor = await _tutores.ObterPorId(id);

            if (tutor == null || tutor.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TutorDTO>.NaoEncontrado("Tutor não encontrado.");
            }

            // O próprio tutor pode manter os dados de contato atualizados.
            if (_usuarioAtual.EhTutor && tutor.Id != _usuarioAtual.TutorId)
            {
                return Resultado<TutorDTO>.NaoAutorizado("Você só pode alterar o seu próprio cadastro.");
            }

            tutor.Telefone = dto.Telefone.Trim();
            tutor.Endereco = dto.Endereco.Trim();
            tutor.Cpf = dto.Cpf.Trim();

            _tutores.Atualizar(tutor);
            await _tutores.SalvarAlteracoes();

            return Resultado<TutorDTO>.Ok(MapearParaDTO(tutor), "Tutor atualizado com sucesso.");
        }

        private static TutorDTO MapearParaDTO(Tutor tutor)
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
                QuantidadePets = tutor.Pets?.Count ?? 0
            };
        }
    }
}
