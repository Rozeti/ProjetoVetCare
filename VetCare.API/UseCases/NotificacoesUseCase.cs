using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>HU-015: consulta e leitura das notificações do usuário autenticado.</summary>
    public class NotificacoesUseCase
    {
        private readonly INotificacaoRepository _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public NotificacoesUseCase(INotificacaoRepository notificacoes, UsuarioAtual usuarioAtual)
        {
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<NotificacaoDTO>>> Listar(bool apenasNaoVisualizadas, int limite = 50)
        {
            var limiteSeguro = Math.Clamp(limite, 1, 200);

            var notificacoes = await _notificacoes.ObterPorUsuario(
                _usuarioAtual.Id, apenasNaoVisualizadas, limiteSeguro);

            var dtos = notificacoes.Select(n => new NotificacaoDTO
            {
                Id = n.Id,
                Tipo = n.Tipo,
                Titulo = n.Titulo,
                Conteudo = n.Conteudo,
                LinkRelacionado = n.LinkRelacionado,
                DataCriacao = n.DataCriacao,
                Visualizada = n.Visualizada
            }).ToList();

            return Resultado<List<NotificacaoDTO>>.Ok(dtos);
        }

        public async Task<Resultado<int>> ContarNaoVisualizadas()
        {
            return Resultado<int>.Ok(await _notificacoes.ContarNaoVisualizadas(_usuarioAtual.Id));
        }

        public async Task<Resultado> MarcarComoVisualizada(Guid id)
        {
            var notificacao = await _notificacoes.ObterPorId(id);

            if (notificacao == null)
            {
                return Resultado.NaoEncontrado("Notificação não encontrada.");
            }

            if (notificacao.UsuarioId != _usuarioAtual.Id)
            {
                return Resultado.NaoAutorizado("Esta notificação pertence a outro usuário.");
            }

            notificacao.Visualizada = true;
            await _notificacoes.SalvarAlteracoes();

            return Resultado.Ok("Notificação marcada como visualizada.");
        }

        public async Task<Resultado> MarcarTodasComoVisualizadas()
        {
            var total = await _notificacoes.MarcarTodasComoVisualizadas(_usuarioAtual.Id);
            return Resultado.Ok($"{total} notificação(ões) marcada(s) como visualizada(s).");
        }
    }
}
