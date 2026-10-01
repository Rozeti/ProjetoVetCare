using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class ProntuarioRepository : IProntuarioRepository
    {
        private readonly AppDbContext _context;

        public ProntuarioRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Prontuario?> ObterPorId(Guid id)
        {
            return await _context.Prontuarios
                .Include(p => p.Paciente)
                    .ThenInclude(pa => pa!.Tutor)
                        .ThenInclude(t => t!.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Prontuario?> ObterPorPacienteId(Guid pacienteId)
        {
            return await _context.Prontuarios
                .Include(p => p.Paciente)
                    .ThenInclude(pa => pa!.Tutor)
                        .ThenInclude(t => t!.Usuario)
                .FirstOrDefaultAsync(p => p.PacienteId == pacienteId);
        }

        public async Task<Prontuario> ObterOuCriarPorPacienteId(Guid pacienteId)
        {
            var prontuario = await _context.Prontuarios.FirstOrDefaultAsync(p => p.PacienteId == pacienteId);

            if (prontuario != null)
            {
                return prontuario;
            }

            prontuario = new Prontuario { PacienteId = pacienteId };
            await _context.Prontuarios.AddAsync(prontuario);
            await _context.SaveChangesAsync();

            return prontuario;
        }

        public async Task<List<AvaliacaoClinica>> ObterAvaliacoes(Guid prontuarioId)
        {
            return await _context.AvaliacoesClinicas
                .AsNoTracking()
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(a => a.ProntuarioId == prontuarioId)
                .OrderByDescending(a => a.DataRegistro)
                .ToListAsync();
        }

        public async Task<List<AtendimentoFisioterapeutico>> ObterAtendimentos(Guid prontuarioId)
        {
            return await _context.Atendimentos
                .AsNoTracking()
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Include(a => a.Sessao)
                .Where(a => a.ProntuarioId == prontuarioId)
                .OrderByDescending(a => a.DataRegistro)
                .ToListAsync();
        }

        public async Task<List<MidiaSessao>> ObterMidias(Guid prontuarioId)
        {
            // A mídia se liga à sessão, e a sessão ao tratamento do paciente. Passar por esse
            // caminho — e não pelo atendimento — inclui as fotos anexadas a sessões que ainda
            // não tiveram o atendimento registrado.
            var pacienteId = await _context.Prontuarios
                .Where(p => p.Id == prontuarioId)
                .Select(p => (Guid?)p.PacienteId)
                .FirstOrDefaultAsync();

            if (pacienteId == null)
            {
                return new List<MidiaSessao>();
            }

            return await _context.MidiasSessao
                .AsNoTracking()
                .Where(m => m.Sessao!.Tratamento!.PacienteId == pacienteId.Value)
                .OrderByDescending(m => m.DataUpload)
                .ToListAsync();
        }

        public async Task<List<ObservacaoInterna>> ObterObservacoesInternas(Guid prontuarioId)
        {
            return await _context.ObservacoesInternas
                .AsNoTracking()
                .Include(o => o.Autor)
                .Where(o => o.ProntuarioId == prontuarioId)
                .OrderByDescending(o => o.DataRegistro)
                .ToListAsync();
        }

        public async Task<List<DocumentoClinico>> ObterDocumentos(Guid prontuarioId)
        {
            return await _context.DocumentosClinicos
                .AsNoTracking()
                .Include(d => d.EnviadoPor)
                .Where(d => d.ProntuarioId == prontuarioId)
                .OrderByDescending(d => d.DataUpload)
                .ToListAsync();
        }

        public async Task Adicionar(Prontuario prontuario)
        {
            await _context.Prontuarios.AddAsync(prontuario);
        }

        public void Atualizar(Prontuario prontuario)
        {
            _context.Prontuarios.Update(prontuario);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
