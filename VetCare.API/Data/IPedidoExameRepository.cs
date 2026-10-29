using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IPedidoExameRepository
    {
        Task Adicionar(PedidoExame pedido);

        /// <summary>Carrega também o paciente, o tutor, a avaliação e o veterinário, usados na impressão.</summary>
        Task<PedidoExame?> ObterPorId(Guid id);

        Task<List<PedidoExame>> ObterPorProntuario(Guid prontuarioId);
        void Atualizar(PedidoExame pedido);
        Task SalvarAlteracoes();
    }
}
