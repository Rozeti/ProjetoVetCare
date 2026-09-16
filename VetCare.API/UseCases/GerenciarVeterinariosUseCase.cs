using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    public class GerenciarVeterinariosUseCase
    {
        /// <summary>
        /// HU-005, CA-1: paleta fixa usada para diferenciar cada profissional na agenda geral.
        /// A cor é derivada da posição do veterinário na lista, o que a mantém estável entre
        /// consultas sem precisar persistir a escolha.
        /// </summary>
        private static readonly string[] Paleta =
        {
            "#0284c7", "#7c3aed", "#059669", "#d97706",
            "#db2777", "#0891b2", "#65a30d", "#dc2626"
        };

        private readonly IVeterinarioRepository _veterinarios;
        private readonly IUsuarioRepository _usuarios;
        private readonly PasswordHasher _hasher;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarVeterinariosUseCase(
            IVeterinarioRepository veterinarios,
            IUsuarioRepository usuarios,
            PasswordHasher hasher,
            UsuarioAtual usuarioAtual)
        {
            _veterinarios = veterinarios;
            _usuarios = usuarios;
            _hasher = hasher;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<VeterinarioDTO>>> Listar()
        {
            var veterinarios = await _veterinarios.Listar(_usuarioAtual.ClinicaId);
            return Resultado<List<VeterinarioDTO>>.Ok(MapearLista(veterinarios));
        }

        public async Task<Resultado<VeterinarioDTO>> ObterPorId(Guid id)
        {
            var veterinario = await _veterinarios.ObterPorId(id);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VeterinarioDTO>.NaoEncontrado("Veterinário não encontrado.");
            }

            return Resultado<VeterinarioDTO>.Ok(MapearParaDTO(veterinario, 0));
        }

        public async Task<Resultado<VeterinarioDTO>> Cadastrar(CriarVeterinarioDTO dto)
        {
            if (await _veterinarios.CrmvExiste(dto.Crmv))
            {
                return Resultado<VeterinarioDTO>.Conflito("Já existe um veterinário cadastrado com este CRMV.");
            }

            Usuario usuario;

            if (dto.UsuarioId.HasValue && dto.UsuarioId.Value != Guid.Empty)
            {
                var existente = await _usuarios.ObterPorId(dto.UsuarioId.Value);

                if (existente == null || existente.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<VeterinarioDTO>.NaoEncontrado("Usuário informado não encontrado.");
                }

                if (await _veterinarios.ObterPorUsuarioId(existente.Id) != null)
                {
                    return Resultado<VeterinarioDTO>.Conflito("Este usuário já possui um cadastro de veterinário.");
                }

                usuario = existente;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Email))
                {
                    return Resultado<VeterinarioDTO>.Invalido(
                        "Informe nome e e-mail para criar o acesso do veterinário, ou envie o UsuarioId de um usuário existente.");
                }

                if (await _usuarios.EmailExiste(dto.Email))
                {
                    return Resultado<VeterinarioDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
                }

                var senha = string.IsNullOrWhiteSpace(dto.Senha) ? _hasher.GerarSenhaProvisoria() : dto.Senha;

                usuario = new Usuario
                {
                    ClinicaId = _usuarioAtual.ClinicaId,
                    Nome = dto.Nome.Trim(),
                    Email = dto.Email.Trim().ToLowerInvariant(),
                    SenhaHash = _hasher.Gerar(senha),
                    Perfil = Perfis.Veterinario
                };

                // Gravado junto com o veterinário, mais abaixo, para que um usuário
                // nunca fique sem o cadastro correspondente.
                await _usuarios.Adicionar(usuario);
            }

            var veterinario = new Veterinario
            {
                UsuarioId = usuario.Id,
                Crmv = dto.Crmv.Trim(),
                Especialidade = string.IsNullOrWhiteSpace(dto.Especialidade)
                    ? "Fisioterapia veterinária"
                    : dto.Especialidade.Trim()
            };

            await _veterinarios.Adicionar(veterinario);
            await _veterinarios.SalvarAlteracoes();

            veterinario.Usuario = usuario;

            return Resultado<VeterinarioDTO>.Ok(MapearParaDTO(veterinario, 0), "Veterinário cadastrado com sucesso.");
        }

        public async Task<Resultado<VeterinarioDTO>> Atualizar(Guid id, AtualizarVeterinarioDTO dto)
        {
            var veterinario = await _veterinarios.ObterPorId(id);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VeterinarioDTO>.NaoEncontrado("Veterinário não encontrado.");
            }

            if (await _veterinarios.CrmvExiste(dto.Crmv, id))
            {
                return Resultado<VeterinarioDTO>.Conflito("Já existe outro veterinário cadastrado com este CRMV.");
            }

            veterinario.Crmv = dto.Crmv.Trim();
            veterinario.Especialidade = dto.Especialidade.Trim();

            _veterinarios.Atualizar(veterinario);
            await _veterinarios.SalvarAlteracoes();

            return Resultado<VeterinarioDTO>.Ok(MapearParaDTO(veterinario, 0), "Veterinário atualizado com sucesso.");
        }

        public static List<VeterinarioDTO> MapearLista(List<Veterinario> veterinarios)
        {
            return veterinarios
                .Select((veterinario, indice) => MapearParaDTO(veterinario, indice))
                .ToList();
        }

        public static string ObterCor(int indice) => Paleta[Math.Abs(indice) % Paleta.Length];

        private static VeterinarioDTO MapearParaDTO(Veterinario veterinario, int indice)
        {
            return new VeterinarioDTO
            {
                Id = veterinario.Id,
                UsuarioId = veterinario.UsuarioId,
                Nome = veterinario.Usuario?.Nome ?? string.Empty,
                Email = veterinario.Usuario?.Email ?? string.Empty,
                Crmv = veterinario.Crmv,
                Especialidade = veterinario.Especialidade,
                Ativo = veterinario.Usuario?.Ativo ?? false,
                Cor = ObterCor(indice)
            };
        }
    }
}
