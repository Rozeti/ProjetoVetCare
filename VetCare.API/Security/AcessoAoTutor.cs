using VetCare.API.Models;

namespace VetCare.API.Security
{
    /// <summary>
    /// Quem pode ver um tutor. O tutor acompanha os pacientes: como toda a equipe clínica
    /// alcança qualquer paciente da clínica, alcança também qualquer tutor. O Tutor enxerga
    /// apenas o próprio cadastro.
    /// </summary>
    public static class AcessoAoTutor
    {
        public static bool Permitido(UsuarioAtual usuario, Tutor? tutor)
        {
            if (tutor == null)
            {
                return false;
            }

            if (usuario.EhTutor)
            {
                return usuario.TutorId.HasValue && usuario.TutorId.Value == tutor.Id;
            }

            return usuario.EhAdministrador || usuario.EhApoio || usuario.EhVeterinario;
        }

        /// <summary>Os pacientes do tutor que aparecem na tela de tutores: todos os dele.</summary>
        public static List<Pet> PacientesVisiveis(Tutor tutor) => (tutor.Pets ?? new List<Pet>()).ToList();
    }
}
