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
                .Include(p => p.AlergiasCondicoes.Where(a => a.Ativa))
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PaginaDe<Pet>> Listar(
            Guid clinicaId,
            Guid? tutorId,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            var consulta = _context.Pets
                .AsNoTracking()
                .Include(p => p.Tutor).ThenInclude(t => t!.Usuario)
                .Include(p => p.AlergiasCondicoes.Where(a => a.Ativa))
                .Where(p => p.ClinicaId == clinicaId);

            if (tutorId.HasValue)
            {
                consulta = consulta.Where(p => p.TutorId == tutorId.Value);
            }

            if (ativo.HasValue)
            {
                consulta = consulta.Where(p => p.Ativo == ativo.Value);
            }

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLowerInvariant();

                consulta = consulta.Where(p =>
                    p.Nome.ToLower().Contains(termo) ||
                    p.Especie.ToLower().Contains(termo) ||
                    p.Raca.ToLower().Contains(termo) ||
                    p.Microchip.ToLower().Contains(termo) ||
                    p.Tutor!.Usuario!.Nome.ToLower().Contains(termo));
            }

            return await consulta.OrderBy(p => p.Nome).Paginar(parametros);
        }

        public async Task<List<Pet>> ObterPorTutor(Guid tutorId)
        {
            return await _context.Pets
                .AsNoTracking()
                .Include(p => p.AlergiasCondicoes.Where(a => a.Ativa))
                .Where(p => p.TutorId == tutorId && p.Ativo)
                .OrderBy(p => p.Nome)
                .ToListAsync();
        }

        public void Atualizar(Pet pet) => _context.Pets.Update(pet);

        public async Task<bool> PossuiRegistrosClinicos(Guid petId)
        {
            var temProntuarioComRegistros = await _context.Prontuarios
                .Where(p => p.PacienteId == petId)
                .AnyAsync(p => _context.AvaliacoesClinicas.Any(a => a.ProntuarioId == p.Id) ||
                               _context.Atendimentos.Any(a => a.ProntuarioId == p.Id) ||
                               _context.Prescricoes.Any(pr => pr.ProntuarioId == p.Id));

            if (temProntuarioComRegistros)
            {
                return true;
            }

            return await _context.Tratamentos.AnyAsync(t => t.PacienteId == petId) ||
                   await _context.Vacinas.AnyAsync(v => v.PacienteId == petId);
        }

        public async Task<bool> MicrochipEmUso(Guid clinicaId, string microchip, Guid? ignorarPetId = null)
        {
            var normalizado = microchip.Trim().ToLowerInvariant();

            return await _context.Pets.AnyAsync(p =>
                p.ClinicaId == clinicaId &&
                p.Microchip.ToLower() == normalizado &&
                (ignorarPetId == null || p.Id != ignorarPetId));
        }

        public async Task<int> ContarAtivos(Guid clinicaId) =>
            await _context.Pets.CountAsync(p => p.ClinicaId == clinicaId && p.Ativo);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
