using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    public class AutenticarUsuarioUseCase
    {
        /// <summary>RN-006: bloqueio temporário após 5 tentativas malsucedidas consecutivas.</summary>
        private const int MaximoTentativas = 5;
        private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

        private const string MensagemCredenciaisInvalidas = "E-mail ou senha inválidos.";

        private readonly IUsuarioRepository _repositorio;
        private readonly IVeterinarioRepository _veterinarioRepositorio;
        private readonly ITutorRepository _tutorRepositorio;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher _hasher;
        private readonly AuditoriaService _auditoria;

        public AutenticarUsuarioUseCase(
            IUsuarioRepository repositorio,
            IVeterinarioRepository veterinarioRepositorio,
            ITutorRepository tutorRepositorio,
            TokenService tokenService,
            PasswordHasher hasher,
            AuditoriaService auditoria)
        {
            _repositorio = repositorio;
            _veterinarioRepositorio = veterinarioRepositorio;
            _tutorRepositorio = tutorRepositorio;
            _tokenService = tokenService;
            _hasher = hasher;
            _auditoria = auditoria;
        }

        public async Task<Resultado<LoginRespostaDTO>> Executar(LoginDTO dto)
        {
            var usuario = await _repositorio.ObterPorEmail(dto.Email);

            // HU-001, CA-2: a mensagem é genérica e não revela qual campo está incorreto.
            if (usuario == null)
            {
                await _auditoria.RegistrarTentativaAnonima(AuditoriaService.Acoes.LoginNegado, dto.Email);
                return Resultado<LoginRespostaDTO>.NaoAutenticado(MensagemCredenciaisInvalidas);
            }

            if (usuario.BloqueadoAte.HasValue && usuario.BloqueadoAte.Value > DateTime.UtcNow)
            {
                var minutosRestantes = Math.Max(1, (int)(usuario.BloqueadoAte.Value - DateTime.UtcNow).TotalMinutes);

                await Auditar(usuario, AuditoriaService.Acoes.LoginNegado, "Conta bloqueada por excesso de tentativas");

                return Resultado<LoginRespostaDTO>.Bloqueado(
                    $"Acesso temporariamente bloqueado por excesso de tentativas. Tente novamente em {minutosRestantes} minuto(s).");
            }

            var senhaCorreta = _hasher.Verificar(dto.Senha, usuario.SenhaHash, out var precisaRehash);

            if (!senhaCorreta)
            {
                await RegistrarTentativaFalha(usuario);
                await Auditar(usuario, AuditoriaService.Acoes.LoginNegado, "Senha incorreta");

                return Resultado<LoginRespostaDTO>.NaoAutenticado(MensagemCredenciaisInvalidas);
            }

            // A senha correta zera o contador mesmo numa conta inativa: quando ela for
            // reativada, não deve carregar um bloqueio antigo.
            usuario.TentativasFalhas = 0;
            usuario.BloqueadoAte = null;

            // HU-001, CA-3: conta desativada recebe aviso específico, mesmo com credenciais corretas.
            if (!usuario.Ativo)
            {
                _repositorio.Atualizar(usuario);
                await _repositorio.SalvarAlteracoes();
                await Auditar(usuario, AuditoriaService.Acoes.LoginNegado, "Conta inativa");

                return Resultado<LoginRespostaDTO>.NaoAutenticado(
                    "Esta conta está inativa. Procure o administrador da clínica.");
            }

            usuario.UltimoAcesso = DateTime.UtcNow;

            if (precisaRehash)
            {
                usuario.SenhaHash = _hasher.Gerar(dto.Senha);
            }

            _repositorio.Atualizar(usuario);
            await _repositorio.SalvarAlteracoes();

            var veterinario = usuario.Perfil == Perfis.Veterinario
                ? await _veterinarioRepositorio.ObterPorUsuarioId(usuario.Id)
                : null;

            var tutor = usuario.Perfil == Perfis.Tutor
                ? await _tutorRepositorio.ObterPorUsuarioId(usuario.Id)
                : null;

            var token = _tokenService.GerarToken(usuario, veterinario?.Id, tutor?.Id);

            await Auditar(usuario, AuditoriaService.Acoes.Login, "Login realizado");

            var resposta = new LoginRespostaDTO
            {
                Token = token,
                ExpiraEm = DateTime.UtcNow.AddHours(_tokenService.HorasValidade),
                Usuario = MapearParaDTO(usuario, veterinario, tutor)
            };

            return Resultado<LoginRespostaDTO>.Ok(resposta, "Login realizado com sucesso.");
        }

        private async Task RegistrarTentativaFalha(Usuario usuario)
        {
            usuario.TentativasFalhas++;

            if (usuario.TentativasFalhas >= MaximoTentativas)
            {
                usuario.BloqueadoAte = DateTime.UtcNow.Add(DuracaoBloqueio);
                usuario.TentativasFalhas = 0;
            }

            _repositorio.Atualizar(usuario);
            await _repositorio.SalvarAlteracoes();
        }

        private Task Auditar(Usuario usuario, string acao, string detalhe) =>
            _auditoria.RegistrarDe(usuario, acao, "Autenticacao", usuario.Id, detalhe);

        private static UsuarioDTO MapearParaDTO(Usuario usuario, Veterinario? veterinario, Tutor? tutor)
        {
            return new UsuarioDTO
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Perfil = usuario.Perfil,
                Ativo = usuario.Ativo,
                DataCadastro = usuario.DataCadastro,
                UltimoAcesso = usuario.UltimoAcesso,
                NotificarPorEmail = usuario.NotificarPorEmail,
                NotificarPorPush = usuario.NotificarPorPush,
                VeterinarioId = veterinario?.Id,
                Crmv = veterinario?.Crmv,
                Especialidade = veterinario?.Especialidade,
                TutorId = tutor?.Id,
                Telefone = tutor?.Telefone,
                Endereco = tutor?.Endereco
            };
        }
    }
}
