using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Troca do veterinário responsável por um paciente. Serve tanto para designar o
    /// primeiro responsável (cadastro vindo do aplicativo) quanto para transferir um
    /// paciente que o profissional atual não vai mais atender.
    /// </summary>
    public class TransferirPacienteDTO
    {
        [Required(ErrorMessage = "Informe o veterinário que passa a ser responsável.")]
        public Guid VeterinarioId { get; set; }

        [StringLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
        public string Motivo { get; set; } = string.Empty;
    }

    /// <summary>O que mudou de mãos na transferência, para a tela confirmar ao usuário.</summary>
    public class ResultadoTransferenciaDTO
    {
        public PetDTO Paciente { get; set; } = new();
        public string NomeVeterinarioAnterior { get; set; } = string.Empty;
        public string NomeNovoVeterinario { get; set; } = string.Empty;
        public int TratamentosTransferidos { get; set; }
        public int SessoesTransferidas { get; set; }
    }
}
