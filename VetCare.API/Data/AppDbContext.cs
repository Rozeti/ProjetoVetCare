using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Clinica> Clinicas => Set<Clinica>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Tutor> Tutores => Set<Tutor>();
        public DbSet<Veterinario> Veterinarios => Set<Veterinario>();
        public DbSet<ApoioAdministrativo> ApoiosAdministrativos => Set<ApoioAdministrativo>();
        public DbSet<Pet> Pets => Set<Pet>();
        public DbSet<AlergiaCondicao> AlergiasCondicoes => Set<AlergiaCondicao>();
        public DbSet<Vacina> Vacinas => Set<Vacina>();
        public DbSet<Tratamento> Tratamentos => Set<Tratamento>();
        public DbSet<Sessao> Sessoes => Set<Sessao>();
        public DbSet<BloqueioAgenda> BloqueiosAgenda => Set<BloqueioAgenda>();
        public DbSet<Prontuario> Prontuarios => Set<Prontuario>();
        public DbSet<AvaliacaoClinica> AvaliacoesClinicas => Set<AvaliacaoClinica>();
        public DbSet<AtendimentoFisioterapeutico> Atendimentos => Set<AtendimentoFisioterapeutico>();
        public DbSet<Prescricao> Prescricoes => Set<Prescricao>();
        public DbSet<ItemPrescricao> ItensPrescricao => Set<ItemPrescricao>();
        public DbSet<MidiaSessao> MidiasSessao => Set<MidiaSessao>();
        public DbSet<ObservacaoInterna> ObservacoesInternas => Set<ObservacaoInterna>();
        public DbSet<DocumentoClinico> DocumentosClinicos => Set<DocumentoClinico>();
        public DbSet<Mensagem> Mensagens => Set<Mensagem>();
        public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
        public DbSet<VersaoRegistroClinico> VersoesRegistrosClinicos => Set<VersaoRegistroClinico>();
        public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
        public DbSet<TokenRedefinicaoSenha> TokensRedefinicaoSenha => Set<TokenRedefinicaoSenha>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            ConfigurarClinicaEUsuarios(builder);
            ConfigurarPacientes(builder);
            ConfigurarAgenda(builder);
            ConfigurarProntuario(builder);
            ConfigurarComunicacao(builder);
            ConfigurarRastreabilidade(builder);

            // Registros monetários e de medida usam precisão fixa em vez do padrão do
            // provedor, que varia entre bancos.
            foreach (var propriedade in builder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                propriedade.SetColumnType("numeric(8,2)");
            }

            // Nenhum registro do domínio é apagado em cascata: a exclusão precisa ser
            // uma decisão explícita, o que preserva a integridade do histórico (RN-004).
            foreach (var relacionamento in builder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetForeignKeys())
                         .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership))
            {
                relacionamento.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        private static void ConfigurarClinicaEUsuarios(ModelBuilder builder)
        {
            builder.Entity<Clinica>(entidade =>
            {
                entidade.Property(c => c.Nome).HasMaxLength(150).IsRequired();
                entidade.Property(c => c.Cnpj).HasMaxLength(20);
                entidade.Property(c => c.Telefone).HasMaxLength(30);
                entidade.Property(c => c.Endereco).HasMaxLength(250);
            });

            builder.Entity<Usuario>(entidade =>
            {
                entidade.Property(u => u.Nome).HasMaxLength(120).IsRequired();
                entidade.Property(u => u.Email).HasMaxLength(180).IsRequired();
                entidade.Property(u => u.Perfil).HasMaxLength(20).IsRequired();
                entidade.Property(u => u.SenhaHash).HasMaxLength(255).IsRequired();

                // RN-007: o e-mail é a credencial de acesso e precisa ser único.
                entidade.HasIndex(u => u.Email).IsUnique();
                entidade.HasIndex(u => new { u.ClinicaId, u.Perfil });

                entidade.HasOne(u => u.Clinica)
                        .WithMany()
                        .HasForeignKey(u => u.ClinicaId);
            });

            builder.Entity<Tutor>(entidade =>
            {
                entidade.Property(t => t.Telefone).HasMaxLength(30);
                entidade.Property(t => t.Endereco).HasMaxLength(250);
                entidade.Property(t => t.Cpf).HasMaxLength(20);

                entidade.HasIndex(t => t.UsuarioId).IsUnique();
                entidade.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId);
            });

            builder.Entity<Veterinario>(entidade =>
            {
                entidade.Property(v => v.Crmv).HasMaxLength(30).IsRequired();
                entidade.Property(v => v.Especialidade).HasMaxLength(120);

                entidade.HasIndex(v => v.UsuarioId).IsUnique();
                entidade.HasIndex(v => v.Crmv).IsUnique();
                entidade.HasOne(v => v.Usuario).WithMany().HasForeignKey(v => v.UsuarioId);
            });

            builder.Entity<ApoioAdministrativo>(entidade =>
            {
                entidade.Property(a => a.Setor).HasMaxLength(80);

                entidade.HasIndex(a => a.UsuarioId).IsUnique();
                entidade.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId);
            });

            builder.Entity<TokenRedefinicaoSenha>(entidade =>
            {
                entidade.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
                entidade.Ignore(t => t.Valido);

                entidade.HasIndex(t => t.TokenHash).IsUnique();
                entidade.HasIndex(t => t.UsuarioId);
                entidade.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId);
            });
        }

        private static void ConfigurarPacientes(ModelBuilder builder)
        {
            builder.Entity<Pet>(entidade =>
            {
                entidade.Property(p => p.Nome).HasMaxLength(80).IsRequired();
                entidade.Property(p => p.Especie).HasMaxLength(40).IsRequired();
                entidade.Property(p => p.Raca).HasMaxLength(80);
                entidade.Property(p => p.Sexo).HasMaxLength(20);
                entidade.Property(p => p.Pelagem).HasMaxLength(60);
                entidade.Property(p => p.Microchip).HasMaxLength(40);

                entidade.HasIndex(p => new { p.ClinicaId, p.Ativo });
                entidade.HasIndex(p => p.TutorId);

                // RN-001: um paciente pertence a exatamente um tutor responsável.
                entidade.HasOne(p => p.Tutor)
                        .WithMany(t => t.Pets)
                        .HasForeignKey(p => p.TutorId);

                entidade.HasOne(p => p.Clinica).WithMany().HasForeignKey(p => p.ClinicaId);
            });

            builder.Entity<AlergiaCondicao>(entidade =>
            {
                entidade.Property(a => a.Tipo).HasMaxLength(30).IsRequired();
                entidade.Property(a => a.Descricao).HasMaxLength(400).IsRequired();
                entidade.Property(a => a.Gravidade).HasMaxLength(20);

                entidade.HasIndex(a => new { a.PacienteId, a.Ativa });

                entidade.HasOne(a => a.Paciente)
                        .WithMany(p => p.AlergiasCondicoes)
                        .HasForeignKey(a => a.PacienteId);

                entidade.HasOne(a => a.RegistradoPor).WithMany().HasForeignKey(a => a.RegistradoPorId);
            });

            builder.Entity<Vacina>(entidade =>
            {
                entidade.Property(v => v.Tipo).HasMaxLength(30).IsRequired();
                entidade.Property(v => v.Nome).HasMaxLength(120).IsRequired();
                entidade.Property(v => v.Fabricante).HasMaxLength(120);
                entidade.Property(v => v.Lote).HasMaxLength(60);
                entidade.Property(v => v.Observacoes).HasMaxLength(500);

                entidade.HasIndex(v => v.PacienteId);

                // Consulta do serviço que avisa sobre doses a vencer.
                entidade.HasIndex(v => new { v.ProximaDose, v.LembreteEnviado });

                entidade.HasOne(v => v.Paciente)
                        .WithMany(p => p.Vacinas)
                        .HasForeignKey(v => v.PacienteId);

                entidade.HasOne(v => v.Veterinario)
                        .WithMany()
                        .HasForeignKey(v => v.VeterinarioId)
                        .IsRequired(false);
            });
        }

        private static void ConfigurarAgenda(ModelBuilder builder)
        {
            builder.Entity<Tratamento>(entidade =>
            {
                entidade.Property(t => t.ObjetivoTerapeutico).HasMaxLength(1000).IsRequired();
                entidade.Property(t => t.ObservacoesGerais).HasMaxLength(2000);
                entidade.Property(t => t.Status).HasMaxLength(20).IsRequired();

                entidade.HasIndex(t => new { t.PacienteId, t.Status });
                entidade.HasIndex(t => t.VeterinarioId);

                entidade.HasOne(t => t.Paciente).WithMany().HasForeignKey(t => t.PacienteId);
                entidade.HasOne(t => t.Veterinario).WithMany().HasForeignKey(t => t.VeterinarioId);
            });

            builder.Entity<Sessao>(entidade =>
            {
                entidade.Property(s => s.Status).HasMaxLength(30).IsRequired();
                entidade.Property(s => s.Observacoes).HasMaxLength(1000);

                // RNF-004: a agenda é sempre consultada por veterinário dentro de um período.
                entidade.HasIndex(s => new { s.VeterinarioId, s.DataHora });
                entidade.HasIndex(s => new { s.TratamentoId, s.DataHora });

                entidade.HasOne(s => s.Tratamento)
                        .WithMany(t => t.Sessoes)
                        .HasForeignKey(s => s.TratamentoId);

                entidade.HasOne(s => s.Veterinario).WithMany().HasForeignKey(s => s.VeterinarioId);
            });

            builder.Entity<BloqueioAgenda>(entidade =>
            {
                entidade.Property(b => b.Motivo).HasMaxLength(200);

                entidade.HasIndex(b => new { b.VeterinarioId, b.Inicio, b.Fim });
                entidade.HasOne(b => b.Veterinario).WithMany().HasForeignKey(b => b.VeterinarioId);
            });
        }

        private static void ConfigurarProntuario(ModelBuilder builder)
        {
            builder.Entity<Prontuario>(entidade =>
            {
                entidade.HasIndex(p => p.PacienteId).IsUnique();
                entidade.HasOne(p => p.Paciente).WithMany().HasForeignKey(p => p.PacienteId);
            });

            builder.Entity<AvaliacaoClinica>(entidade =>
            {
                entidade.Property(a => a.QueixaPrincipal).HasMaxLength(2000).IsRequired();
                entidade.Property(a => a.Anamnese).HasMaxLength(4000).IsRequired();
                entidade.Property(a => a.ExameFisico).HasMaxLength(4000).IsRequired();
                entidade.Property(a => a.HipoteseDiagnostica).HasMaxLength(2000).IsRequired();
                entidade.Property(a => a.PlanoTerapeutico).HasMaxLength(4000).IsRequired();

                entidade.HasIndex(a => new { a.ProntuarioId, a.DataRegistro });
                entidade.HasIndex(a => new { a.VeterinarioId, a.DataRegistro });

                entidade.HasOne(a => a.Prontuario).WithMany().HasForeignKey(a => a.ProntuarioId);
                entidade.HasOne(a => a.Tratamento).WithMany().HasForeignKey(a => a.TratamentoId);
                entidade.HasOne(a => a.Veterinario).WithMany().HasForeignKey(a => a.VeterinarioId);
            });

            builder.Entity<AtendimentoFisioterapeutico>(entidade =>
            {
                entidade.Property(a => a.TecnicasAplicadas).HasMaxLength(1000).IsRequired();
                entidade.Property(a => a.EvolucaoClinica).HasMaxLength(4000).IsRequired();
                entidade.Property(a => a.SinaisVitais).HasMaxLength(500);
                entidade.Property(a => a.ProximosPassos).HasMaxLength(2000);

                entidade.HasIndex(a => new { a.ProntuarioId, a.DataRegistro });
                entidade.HasIndex(a => new { a.VeterinarioId, a.DataRegistro });

                // Uma sessão gera no máximo um atendimento.
                entidade.HasIndex(a => a.SessaoId).IsUnique();

                entidade.HasOne(a => a.Prontuario).WithMany().HasForeignKey(a => a.ProntuarioId);
                entidade.HasOne(a => a.Sessao).WithMany().HasForeignKey(a => a.SessaoId);
                entidade.HasOne(a => a.Tratamento).WithMany().HasForeignKey(a => a.TratamentoId);
                entidade.HasOne(a => a.Veterinario).WithMany().HasForeignKey(a => a.VeterinarioId);
            });

            builder.Entity<Prescricao>(entidade =>
            {
                entidade.Property(p => p.Orientacoes).HasMaxLength(2000);
                entidade.Property(p => p.Status).HasMaxLength(20).IsRequired();

                entidade.HasIndex(p => new { p.ProntuarioId, p.DataEmissao });

                entidade.HasOne(p => p.Prontuario).WithMany().HasForeignKey(p => p.ProntuarioId);
                entidade.HasOne(p => p.Paciente).WithMany().HasForeignKey(p => p.PacienteId);
                entidade.HasOne(p => p.Veterinario).WithMany().HasForeignKey(p => p.VeterinarioId);
            });

            builder.Entity<ItemPrescricao>(entidade =>
            {
                entidade.Property(i => i.Medicamento).HasMaxLength(200).IsRequired();
                entidade.Property(i => i.Dosagem).HasMaxLength(120);
                entidade.Property(i => i.Frequencia).HasMaxLength(120);
                entidade.Property(i => i.Duracao).HasMaxLength(120);
                entidade.Property(i => i.Via).HasMaxLength(60);
                entidade.Property(i => i.Observacao).HasMaxLength(500);

                // Os itens não existem fora da receita: aqui a exclusão em cascata é correta.
                entidade.HasOne(i => i.Prescricao)
                        .WithMany(p => p.Itens)
                        .HasForeignKey(i => i.PrescricaoId)
                        .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MidiaSessao>(entidade =>
            {
                entidade.Property(m => m.Tipo).HasMaxLength(20).IsRequired();
                entidade.Property(m => m.NomeArquivo).HasMaxLength(255).IsRequired();
                entidade.Property(m => m.UrlArquivo).HasMaxLength(500).IsRequired();

                entidade.HasIndex(m => m.SessaoId);

                entidade.HasOne(m => m.Sessao).WithMany().HasForeignKey(m => m.SessaoId);

                entidade.HasOne(m => m.Atendimento)
                        .WithMany()
                        .HasForeignKey(m => m.AtendimentoId)
                        .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ObservacaoInterna>(entidade =>
            {
                entidade.Property(o => o.Conteudo).HasMaxLength(2000).IsRequired();

                entidade.HasIndex(o => o.ProntuarioId);

                entidade.HasOne(o => o.Prontuario).WithMany().HasForeignKey(o => o.ProntuarioId);
                entidade.HasOne(o => o.Autor).WithMany().HasForeignKey(o => o.AutorId);

                entidade.HasOne(o => o.Avaliacao)
                        .WithMany()
                        .HasForeignKey(o => o.AvaliacaoId)
                        .OnDelete(DeleteBehavior.SetNull);

                entidade.HasOne(o => o.Atendimento)
                        .WithMany()
                        .HasForeignKey(o => o.AtendimentoId)
                        .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<DocumentoClinico>(entidade =>
            {
                entidade.Property(d => d.NomeArquivo).HasMaxLength(255).IsRequired();
                entidade.Property(d => d.TipoDocumento).HasMaxLength(40).IsRequired();
                entidade.Property(d => d.UrlArquivo).HasMaxLength(500).IsRequired();

                entidade.HasIndex(d => d.ProntuarioId);

                entidade.HasOne(d => d.Prontuario).WithMany().HasForeignKey(d => d.ProntuarioId);
                entidade.HasOne(d => d.EnviadoPor).WithMany().HasForeignKey(d => d.EnviadoPorId);
            });
        }

        private static void ConfigurarComunicacao(ModelBuilder builder)
        {
            builder.Entity<Mensagem>(entidade =>
            {
                entidade.Property(m => m.Conteudo).HasMaxLength(2000).IsRequired();

                entidade.HasIndex(m => new { m.RemetenteId, m.DestinatarioId, m.DataEnvio });
                entidade.HasIndex(m => new { m.DestinatarioId, m.Lida });

                entidade.HasOne(m => m.Remetente).WithMany().HasForeignKey(m => m.RemetenteId);
                entidade.HasOne(m => m.Destinatario).WithMany().HasForeignKey(m => m.DestinatarioId);

                entidade.HasOne(m => m.Paciente)
                        .WithMany()
                        .HasForeignKey(m => m.PacienteId)
                        .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Notificacao>(entidade =>
            {
                entidade.Property(n => n.Tipo).HasMaxLength(40).IsRequired();
                entidade.Property(n => n.Titulo).HasMaxLength(150).IsRequired();
                entidade.Property(n => n.Conteudo).HasMaxLength(500).IsRequired();
                entidade.Property(n => n.LinkRelacionado).HasMaxLength(200);

                entidade.HasIndex(n => new { n.UsuarioId, n.Visualizada, n.DataCriacao });

                entidade.HasOne(n => n.Usuario).WithMany().HasForeignKey(n => n.UsuarioId);
            });
        }

        private static void ConfigurarRastreabilidade(ModelBuilder builder)
        {
            builder.Entity<VersaoRegistroClinico>(entidade =>
            {
                entidade.Property(v => v.TipoRegistro).HasMaxLength(60).IsRequired();
                entidade.Property(v => v.ConteudoAnterior).IsRequired();

                entidade.HasIndex(v => new { v.TipoRegistro, v.RegistroId });

                entidade.HasOne(v => v.AlteradoPor).WithMany().HasForeignKey(v => v.AlteradoPorId);
            });

            builder.Entity<RegistroAuditoria>(entidade =>
            {
                entidade.Property(r => r.NomeUsuario).HasMaxLength(120);
                entidade.Property(r => r.Perfil).HasMaxLength(20);
                entidade.Property(r => r.Acao).HasMaxLength(40).IsRequired();
                entidade.Property(r => r.Entidade).HasMaxLength(60);
                entidade.Property(r => r.Detalhe).HasMaxLength(500);
                entidade.Property(r => r.EnderecoIp).HasMaxLength(60);

                entidade.HasIndex(r => new { r.ClinicaId, r.DataHora });
                entidade.HasIndex(r => new { r.UsuarioId, r.DataHora });
                entidade.HasIndex(r => new { r.Entidade, r.EntidadeId });
            });
        }
    }
}
