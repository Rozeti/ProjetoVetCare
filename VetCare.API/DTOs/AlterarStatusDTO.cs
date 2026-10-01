namespace VetCare.API.DTOs
{
    /// <summary>
    /// Ativa ou desativa um registro sem excluí-lo. Serve a usuários (HU-002, CA-4), a
    /// pacientes (HU-003, CA-4) e a alertas clínicos: em todos os casos o histórico fica
    /// preservado e só a visibilidade muda.
    /// </summary>
    public class AlterarStatusDTO
    {
        public bool Ativo { get; set; }
    }
}
