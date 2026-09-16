using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using VetCare.API.Data;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.Tests.Suporte
{
    /// <summary>
    /// Fábricas dos serviços de apoio que os casos de uso exigem, mas cujo
    /// comportamento não é o objeto do teste.
    /// </summary>
    public static class Dependencias
    {
        /// <summary>
        /// O acessor é criado sem definir o HttpContext de propósito. O
        /// <see cref="HttpContextAccessor"/> guarda o contexto num AsyncLocal estático
        /// compartilhado por todas as instâncias: atribuir um contexto novo aqui
        /// apagaria as claims que o <see cref="UsuarioAtual"/> do teste acabou de
        /// publicar. Deixando em branco, ambos leem o mesmo contexto ambiente — que é
        /// exatamente o que acontece em produção, com um contexto por requisição.
        /// </summary>
        public static AuditoriaService Auditoria(AppDbContext contexto, UsuarioAtual usuarioAtual) =>
            new(new AuditoriaRepository(contexto),
                usuarioAtual,
                new HttpContextAccessor(),
                NullLogger<AuditoriaService>.Instance);

        public static NotificacaoService Notificacoes(AppDbContext contexto) =>
            new(new NotificacaoRepository(contexto));
    }
}
