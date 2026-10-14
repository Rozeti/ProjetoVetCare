using VetCare.API.Models;

namespace VetCare.API.Security
{
    /// <summary>
    /// Quem pode ver um tutor. O tutor acompanha os pacientes: o veterinário enxerga os
    /// tutores dos pacientes sob sua responsabilidade e, para conseguir cadastrar um
    /// paciente novo, também os tutores que ainda não têm nenhum pet. Quando um paciente
    /// é transferido, o tutor vai junto, sem precisar de nenhum vínculo a mais.
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

            if (usuario.EhVeterinario)
            {
                return usuario.VeterinarioId.HasValue && Vinculado(tutor, usuario.VeterinarioId.Value);
            }

            return true;
        }

        /// <summary>Tutor de algum paciente do veterinário, ou tutor que ainda não tem paciente nenhum.</summary>
        public static bool Vinculado(Tutor tutor, Guid veterinarioId)
        {
            var pets = tutor.Pets ?? new List<Pet>();

            return pets.Count == 0 || pets.Any(p => p.VeterinarioResponsavelId == veterinarioId);
        }

        /// <summary>Quantos pacientes do tutor quem consulta pode ver.</summary>
        public static int ContarPacientesVisiveis(UsuarioAtual usuario, Tutor tutor)
        {
            var pets = tutor.Pets ?? new List<Pet>();

            return usuario.EhVeterinario
                ? pets.Count(p => p.VeterinarioResponsavelId == usuario.VeterinarioId)
                : pets.Count;
        }
    }
}
