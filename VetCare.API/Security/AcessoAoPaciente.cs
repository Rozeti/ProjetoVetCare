using VetCare.API.Models;

namespace VetCare.API.Security
{
    /// <summary>
    /// Quem pode ver e mexer num paciente. A regra fica num só lugar porque ela aparece em
    /// todo caso de uso que parte de um paciente (prontuário, vacinas, receitas, documentos,
    /// tratamentos, sessões): Administrador e Apoio alcançam a clínica inteira, o Tutor só
    /// os próprios pets (HU-013) e o Veterinário só os pacientes sob sua responsabilidade.
    /// </summary>
    public static class AcessoAoPaciente
    {
        public const string MensagemNegada = "Você não tem acesso a este paciente.";

        public static bool Permitido(UsuarioAtual usuario, Guid tutorId, Guid? veterinarioResponsavelId)
        {
            if (usuario.EhTutor)
            {
                return usuario.TutorId.HasValue && usuario.TutorId.Value == tutorId;
            }

            if (usuario.EhVeterinario)
            {
                return usuario.VeterinarioId.HasValue
                       && veterinarioResponsavelId.HasValue
                       && usuario.VeterinarioId.Value == veterinarioResponsavelId.Value;
            }

            return true;
        }

        public static bool Permitido(UsuarioAtual usuario, Pet? pet) =>
            pet != null && Permitido(usuario, pet.TutorId, pet.VeterinarioResponsavelId);
    }
}
