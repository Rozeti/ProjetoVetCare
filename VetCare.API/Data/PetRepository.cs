using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class PetRepository : IPetRepository
    {
        private readonly AppDbContext _context;

        public PetRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(Pet pet) => await _context.Pets.AddAsync(pet);

        public async Task<Pet?> ObterPorId(Guid id) =>
            await _context.Pets.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Pet?> ObterPorIdComTutor(Guid id)
        {
            return await _context.Pets
                .Include(p => p.Tutor).ThenInclude(t => t!.Usuario)
                .Include(p => p.VeterinarioResponsavel).ThenInclude(v => v!.Usuario)
                .Include(p => p.AlergiasCondicoes.Where(a => a.Ativa))
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PaginaDe<Pet>> Listar(Guid clinicaId, FiltroDePacientes filtro, ParametrosPagina parametros)
        {
            var consulta = _context.Pets
                .AsNoTracking()
                .Include(p => p.Tutor).ThenInclude(t => t!.Usuario)
                .Include(p => p.VeterinarioResponsavel).ThenInclude(v => v!.Usuario)
                .Include(p => p.AlergiasCondicoes.Where(a => a.Ativa))
                .Where(p => p.ClinicaId == clinicaId);

            if (filtro.TutorId.HasValue)
            {
                consulta = consulta.Where(p => p.TutorId == filtro.TutorId.Value);
            }

            if (filtro.VeterinarioResponsavelId.HasValue)
            {
                consulta = consulta.Where(p => p.VeterinarioResponsavelId == filtro.VeterinarioResponsavelId.Value);
            }

            if (filtro.ApenasSemResponsavel)
            {
                consulta = consulta.Where(p => p.VeterinarioResponsavelId == null);
            }

            if (filtro.Ativo.HasValue)
            {
                consulta = consulta.Where(p => p.Ativo == filtro.Ativo.Value);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Raca))
            {
                var raca = filtro.Raca.Trim().ToLowerInvariant();
                consulta = consulta.Where(p => p.Raca.ToLower() == raca);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busca))
            {
                var termo = filtro.Busca.Trim().ToLowerInvariant();

                consulta = consulta.Where(p =>
                    p.Nome.ToLower().Contains(termo) ||
                    p.Especie.ToLower().Contains(termo) ||
                    p.Raca.ToLower().Contains(termo) ||
                    p.Microchip.ToLower().Contains(termo) ||
                    p.Tutor!.Usuario!.Nome.ToLower().Contains(termo));
            }

            return await consulta.OrderBy(p => p.Nome).Paginar(parametros);
        }

        public async Task<List<Pet>> ListarParaSelecao(Guid clinicaId, FiltroDePacientes filtro)
        {
            var consulta = _context.Pets
                .AsNoTracking()
                .Include(p => p.Tutor).ThenInclude(t => t!.Usuario)
                .Where(p => p.ClinicaId == clinicaId && p.Ativo && p.DataObito == null);

            if (filtro.TutorId.HasValue)
            {
                consulta = consulta.Where(p => p.TutorId == filtro.TutorId.Value);
            }

            if (filtro.VeterinarioResponsavelId.HasValue)
            {
                consulta = consulta.Where(p => p.VeterinarioResponsavelId == filtro.VeterinarioResponsavelId.Value);
            }

            return await consulta.OrderBy(p => p.Nome).ToListAsync();
        }

        public void Atualizar(Pet pet) => _context.Pets.Update(pet);

        public async Task<List<string>> ListarRacas(Guid clinicaId, FiltroDePacientes filtro)
        {
            var consulta = _context.Pets.AsNoTracking().Where(p => p.ClinicaId == clinicaId && p.Raca != "");

            if (filtro.TutorId.HasValue)
            {
                consulta = consulta.Where(p => p.TutorId == filtro.TutorId.Value);
            }

            if (filtro.VeterinarioResponsavelId.HasValue)
            {
                consulta = consulta.Where(p => p.VeterinarioResponsavelId == filtro.VeterinarioResponsavelId.Value);
            }

            var racas = await consulta.Select(p => p.Raca.Trim()).Distinct().ToListAsync();

            // A mesma raça digitada com maiúsculas diferentes aparece uma vez só.
            return racas
                .Where(r => r.Length > 0)
                .GroupBy(r => r.ToLowerInvariant())
                .Select(g => g.First())
                .OrderBy(r => r, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true))
                .ToList();
        }

        public async Task<bool> MicrochipEmUso(Guid clinicaId, string microchip, Guid? ignorarPetId = null)
        {
            var normalizado = microchip.Trim().ToLowerInvariant();

            return await _context.Pets.AnyAsync(p =>
                p.ClinicaId == clinicaId &&
                p.Microchip.ToLower() == normalizado &&
                (ignorarPetId == null || p.Id != ignorarPetId));
        }

        public async Task<int> ContarAtivos(Guid clinicaId, Guid? veterinarioResponsavelId = null) =>
            await _context.Pets.CountAsync(p =>
                p.ClinicaId == clinicaId &&
                p.Ativo &&
                (veterinarioResponsavelId == null || p.VeterinarioResponsavelId == veterinarioResponsavelId));

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
