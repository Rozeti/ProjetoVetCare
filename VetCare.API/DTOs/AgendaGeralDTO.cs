namespace VetCare.API.DTOs
{
    /// <summary>HU-005: agenda consolidada da clínica com o toggle de profissionais.</summary>
    public class AgendaGeralDTO
    {
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public List<VeterinarioDTO> Veterinarios { get; set; } = new();
        public List<ItemAgendaDTO> Sessoes { get; set; } = new();

        /// <summary>HU-005, CA-3: sinaliza o dia sem atendimentos sem cair em tela de erro.</summary>
        public bool Vazia => Sessoes.Count == 0;
    }
}
