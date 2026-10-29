using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    /// <summary>Filtros da tela de evoluções; os campos nulos não restringem a consulta.</summary>
    public sealed class FiltroDeEvolucoes
    {
        public Guid? PacienteId { get; init; }
        public Guid? TratamentoId { get; init; }

        /// <summary>Profissional que registrou a evolução.</summary>
        public Guid? VeterinarioId { get; init; }

        public int? NumeroSessao { get; init; }

        /// <summary>Início do intervalo (inclusive), já convertido para UTC.</summary>
        public DateTime? InicioUtc { get; init; }

        /// <summary>Fim do intervalo (exclusive), já convertido para UTC.</summary>
        public DateTime? FimUtc { get; init; }

        /// <summary>Da primeira para a última sessão; sem isso, as mais recentes vêm primeiro.</summary>
        public bool OrdemCronologica { get; init; }
    }

    public interface IAtendimentoRepository
    {
        Task Adicionar(AtendimentoFisioterapeutico atendimento);

        /// <summary>Quantas evoluções o tratamento já tem: define o número da próxima sessão.</summary>
        Task<int> ContarPorTratamento(Guid tratamentoId);

        Task<Dictionary<Guid, int>> ContarPorTratamentos(IEnumerable<Guid> tratamentosIds);

        /// <summary>Tela de evoluções: filtros por animal, tratamento, período, profissional e sessão.</summary>
        Task<PaginaDe<AtendimentoFisioterapeutico>> Listar(Guid clinicaId, FiltroDeEvolucoes filtro, ParametrosPagina parametros);

        /// <summary>Carrega também a sessão, o tratamento e o paciente, que decidem a quem o registro pertence.</summary>
        Task<AtendimentoFisioterapeutico?> ObterPorId(Guid id);

        Task<AtendimentoFisioterapeutico?> ObterPorSessao(Guid sessaoId);

        /// <summary>Quais das sessões informadas já têm atendimento, numa só consulta.</summary>
        Task<HashSet<Guid>> ObterSessoesComAtendimento(IEnumerable<Guid> sessoesIds);

        void Atualizar(AtendimentoFisioterapeutico atendimento);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);

        Task SalvarAlteracoes();
    }
}
