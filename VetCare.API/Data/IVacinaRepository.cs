using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IVacinaRepository
    {
        Task Adicionar(Vacina vacina);
        Task<Vacina?> ObterPorId(Guid id);
        Task<List<Vacina>> ObterPorPaciente(Guid pacienteId);
        Task<List<Vacina>> ObterVencendo(Guid clinicaId, int diasDeAntecedencia);

        /// <summary>Doses a vencer que ainda não geraram lembrete para o tutor.</summary>
        Task<List<Vacina>> ObterPendentesDeLembrete(DateTime limite);

        void Atualizar(Vacina vacina);
        void Remover(Vacina vacina);
        Task SalvarAlteracoes();
    }
}
