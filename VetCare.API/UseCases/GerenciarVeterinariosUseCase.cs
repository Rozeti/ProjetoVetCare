using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    public class GerenciarVeterinariosUseCase
    {
        /// <summary>
        /// HU-005, CA-1: paleta fixa usada para diferenciar cada profissional na agenda geral.
        /// A cor sai do próprio identificador do veterinário, então ela não muda quando um
        /// colega entra ou sai da equipe.
        /// </summary>
        private static readonly string[] Paleta =
        {
            "#0284c7", "#7c3aed", "#059669", "#d97706",
            "#db2777", "#0891b2", "#65a30d", "#dc2626"
        };

        private readonly IVeterinarioRepository _veterinarios;
        private readonly IUsuarioRepository _usuarios;
        private readonly PasswordHasher _hasher;
        private readonly ContasService _contas;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarVeterinariosUseCase(
            IVeterinarioRepository veterinarios,
            IUsuarioRepository usuarios,
            PasswordHasher hasher,
            ContasService contas,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _veterinarios = veterinarios;
            _usuarios = usuarios;
            _hasher = hasher;
            _contas = contas;
            _auditoria = auditoria;
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

            return Resultado<VeterinarioDTO>.Ok(MapearParaDTO(veterinario));
        }

        public async Task<Resultado<VeterinarioDTO>> Cadastrar(CriarVeterinarioDTO dto)
        {
            if (await _veterinarios.CrmvExiste(dto.Crmv))
            {
                return Resultado<VeterinarioDTO>.Conflito("Já existe um veterinário cadastrado com este CRMV.");
            }

            Usuario usuario;
            var usuarioNovo = false;
            var senhaDefinidaPelaClinica = !string.IsNullOrWhiteSpace(dto.Senha);

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

                if (senhaDefinidaPelaClinica && PasswordHasher.ValidarForca(dto.Senha) is { } erroDeSenha)
                {
                    return Resultado<VeterinarioDTO>.Invalido(erroDeSenha);
                }

                usuario = new Usuario
                {
                    ClinicaId = _usuarioAtual.ClinicaId,
                    Nome = dto.Nome.Trim(),
                    Email = dto.Email.Trim().ToLowerInvariant(),
                    SenhaHash = _hasher.Gerar(senhaDefinidaPelaClinica ? dto.Senha! : _hasher.GerarSenhaProvisoria()),
                    Perfil = Perfis.Veterinario
                };

                // Gravado junto com o veterinário, mais abaixo, para que um usuário
                // nunca fique sem o cadastro correspondente.
                await _usuarios.Adicionar(usuario);
                usuarioNovo = true;
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

            if (usuarioNovo)
            {
                await _contas.EnviarBoasVindas(usuario, senhaDefinidaPelaClinica);
            }

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Veterinario", veterinario.Id,
                $"Cadastro de {usuario.Nome} (CRMV {veterinario.Crmv}){(usuarioNovo ? " com conta nova" : " para conta existente")}");

            return Resultado<VeterinarioDTO>.Ok(
                MapearParaDTO(veterinario),
                usuarioNovo && !senhaDefinidaPelaClinica
                    ? "Veterinário cadastrado. Ele recebeu por e-mail o link para criar a própria senha."
                    : "Veterinário cadastrado com sucesso.");
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

            // Especialidade omitida mantém a atual; só o que veio preenchido é alterado.
            if (dto.Especialidade != null)
            {
                veterinario.Especialidade = string.IsNullOrWhiteSpace(dto.Especialidade)
                    ? "Fisioterapia veterinária"
                    : dto.Especialidade.Trim();
            }

            _veterinarios.Atualizar(veterinario);
            await _veterinarios.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Veterinario", veterinario.Id,
                $"Edição de {veterinario.Usuario?.Nome} (CRMV {veterinario.Crmv})");

            return Resultado<VeterinarioDTO>.Ok(MapearParaDTO(veterinario), "Veterinário atualizado com sucesso.");
        }

        public static List<VeterinarioDTO> MapearLista(List<Veterinario> veterinarios) =>
            veterinarios.Select(MapearParaDTO).ToList();

        /// <summary>Cor estável do profissional, derivada do seu identificador.</summary>
        public static string ObterCor(Guid veterinarioId)
        {
            var indice = BitConverter.ToUInt32(veterinarioId.ToByteArray(), 0) % (uint)Paleta.Length;
            return Paleta[indice];
        }

        public static VeterinarioDTO MapearParaDTO(Veterinario veterinario)
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
                Cor = ObterCor(veterinario.Id)
            };
        }
    }
}
