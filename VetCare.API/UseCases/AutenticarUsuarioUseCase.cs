using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    public class AutenticarUsuarioUseCase
    {
        /// <summary>RN-006: bloqueio temporário após 5 tentativas malsucedidas consecutivas.</summary>
        private const int MaximoTentativas = 5;
        private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

        private readonly IUsuarioRepository _repositorio;
        private readonly IVeterinarioRepository _veterinarioRepositorio;
        private readonly ITutorRepository _tutorRepositorio;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher _hasher;

        public AutenticarUsuarioUseCase(
            IUsuarioRepository repositorio,
            IVeterinarioRepository veterinarioRepositorio,
            ITutorRepository tutorRepositorio,
            TokenService tokenService,
            PasswordHasher hasher)
        {
            _repositorio = repositorio;
            _veterinarioRepositorio = veterinarioRepositorio;
            _tutorRepositorio = tutorRepositorio;
            _tokenService = tokenService;
            _hasher = hasher;
        }

        public async Task<Resultado<LoginRespostaDTO>> Executar(LoginDTO dto)
        {
            var usuario = await _repositorio.ObterPorEmail(dto.Email);

            // HU-001, CA-2: a mensagem é genérica e não revela qual campo está incorreto.
            if (usuario == null)
            {
                return Resultado<LoginRespostaDTO>.NaoAutenticado("E-mail ou senha inválidos.");
            }

            if (usuario.BloqueadoAte.HasValue && usuario.BloqueadoAte.Value > DateTime.UtcNow)
            {
                var minutosRestantes = Math.Max(1, (int)(usuario.BloqueadoAte.Value - DateTime.UtcNow).TotalMinutes);

                return Resultado<LoginRespostaDTO>.Bloqueado(
                    $"Acesso temporariamente bloqueado por excesso de tentativas. Tente novamente em {minutosRestantes} minuto(s).");
            }

            var senhaCorreta = _hasher.Verificar(dto.Senha, usuario.SenhaHash, out var precisaRehash);

            if (!senhaCorreta)
            {
                await RegistrarTentativaFalha(usuario);
                return Resultado<LoginRespostaDTO>.NaoAutenticado("E-mail ou senha inválidos.");
            }

            // HU-001, CA-3: conta desativada recebe aviso específico, mesmo com credenciais corretas.
            if (!usuario.Ativo)
            {
                return Resultado<LoginRespostaDTO>.NaoAutenticado(
                    "Esta conta está inativa. Procure o administrador da clínica.");
            }

            // Reautenticação bem-sucedida zera o contador e atualiza hashes em formato antigo.
            usuario.TentativasFalhas = 0;
            usuario.BloqueadoAte = null;
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

            var resposta = new LoginRespostaDTO
            {
                Token = token,
                ExpiraEm = DateTime.UtcNow.AddHours(_tokenService.HorasValidade),
                Usuario = MontarUsuarioDto(usuario, veterinario, tutor)
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

        private static UsuarioDTO MontarUsuarioDto(Usuario usuario, Veterinario? veterinario, Tutor? tutor)
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
