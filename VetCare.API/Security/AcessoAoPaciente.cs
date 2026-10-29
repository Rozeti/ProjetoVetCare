using VetCare.API.Models;

namespace VetCare.API.Security
{
    /// <summary>
    /// Quem pode ver e mexer num paciente. A regra fica num só lugar porque ela aparece em
    /// todo caso de uso que parte de um paciente (prontuário, vacinas, receitas, documentos,
    /// tratamentos, sessões).
    ///
    /// A equipe clínica (Administrador, Apoio e todos os Veterinários) alcança qualquer paciente
    /// da clínica: se o veterinário responsável faltar, um colega atende o animal sem que ninguém
    /// precise transferi-lo antes. O responsável continua existindo para organizar a clínica
    /// (painel, filtros), não para restringir o acesso. O Tutor vê só os próprios pets (HU-013).
    /// A clínica de cada registro é conferida por quem chama.
    /// </summary>
    public static class AcessoAoPaciente
    {
        public const string MensagemNegada = "Você não tem acesso a este paciente.";

        public static bool Permitido(UsuarioAtual usuario, Guid tutorId)
        {
            if (usuario.EhTutor)
            {
                return usuario.TutorId.HasValue && usuario.TutorId.Value == tutorId;
            }

            // Só a equipe clínica entra; qualquer outro perfil (ou um token sem perfil) fica de
            // fora, em vez de entrar por omissão.
            return usuario.EhAdministrador || usuario.EhApoio || usuario.EhVeterinario;
        }

        public static bool Permitido(UsuarioAtual usuario, Pet? pet) =>
            pet != null && Permitido(usuario, pet.TutorId);
    }
}
