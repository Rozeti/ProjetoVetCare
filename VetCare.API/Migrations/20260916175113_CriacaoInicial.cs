using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clinicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    HorasMinimasCancelamento = table.Column<int>(type: "integer", nullable: false),
                    HorarioAbertura = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HorarioFechamento = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DuracaoSessaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinicas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeUsuario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnderecoIp = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TentativasFalhas = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoAcesso = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApoiosAdministrativos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Setor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApoiosAdministrativos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApoiosAdministrativos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Conteudo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LinkRelacionado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Visualizada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokensRedefinicaoSenha",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UtilizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRedefinicaoSenha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRedefinicaoSenha_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tutores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tutores_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VersoesRegistrosClinicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoRegistro = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RegistroId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConteudoAnterior = table.Column<string>(type: "text", nullable: false),
                    AlteradoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataAlteracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesRegistrosClinicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesRegistrosClinicos_Usuarios_AlteradoPorId",
                        column: x => x.AlteradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Veterinarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Crmv = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Especialidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veterinarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veterinarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Especie = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Raca = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Sexo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Pelagem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PesoAtualKg = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    Microchip = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Castrado = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataObito = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pets_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pets_Tutores_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloqueiosAgenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CriadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloqueiosAgenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloqueiosAgenda_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlergiasCondicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistradoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Gravidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlergiasCondicoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlergiasCondicoes_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlergiasCondicoes_Usuarios_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemetenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Conteudo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Lida = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensagens_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Mensagens_Usuarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagens_Usuarios_RemetenteId",
                        column: x => x.RemetenteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prontuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimaAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prontuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prontuarios_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tratamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataFim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObjetivoTerapeutico = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObservacoesGerais = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tratamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tratamentos_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tratamentos_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vacinas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Fabricante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Lote = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DataAplicacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProximaDose = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LembreteEnviado = table.Column<bool>(type: "boolean", nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacinas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacinas_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacinas_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentosClinicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnviadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UrlArquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosClinicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosClinicos_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosClinicos_Usuarios_EnviadoPorId",
                        column: x => x.EnviadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prescricoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidaAte = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Orientacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescricoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescricoes_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescricoes_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescricoes_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AvaliacoesClinicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TratamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueixaPrincipal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Anamnese = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ExameFisico = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    HipoteseDiagnostica = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PlanoTerapeutico = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataUltimaEdicao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesClinicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Tratamentos_TratamentoId",
                        column: x => x.TratamentoId,
                        principalTable: "Tratamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TratamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LembreteEnviado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessoes_Tratamentos_TratamentoId",
                        column: x => x.TratamentoId,
                        principalTable: "Tratamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessoes_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensPrescricao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrescricaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Medicamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dosagem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Frequencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Duracao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Via = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPrescricao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPrescricao_Prescricoes_PrescricaoId",
                        column: x => x.PrescricaoId,
                        principalTable: "Prescricoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Atendimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TratamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TecnicasAplicadas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EscalaDor = table.Column<int>(type: "integer", nullable: false),
                    EvolucaoClinica = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SinaisVitais = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProximosPassos = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    TemperaturaCelsius = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    FrequenciaCardiaca = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaRespiratoria = table.Column<int>(type: "integer", nullable: true),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataUltimaEdicao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atendimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Sessoes_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "Sessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Tratamentos_TratamentoId",
                        column: x => x.TratamentoId,
                        principalTable: "Tratamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MidiasSessao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UrlArquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MidiasSessao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MidiasSessao_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MidiasSessao_Sessoes_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "Sessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObservacoesInternas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvaliacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Conteudo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservacoesInternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObservacoesInternas_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ObservacoesInternas_AvaliacoesClinicas_AvaliacaoId",
                        column: x => x.AvaliacaoId,
                        principalTable: "AvaliacoesClinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ObservacoesInternas_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObservacoesInternas_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlergiasCondicoes_PacienteId_Ativa",
                table: "AlergiasCondicoes",
                columns: new[] { "PacienteId", "Ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_AlergiasCondicoes_RegistradoPorId",
                table: "AlergiasCondicoes",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ApoiosAdministrativos_UsuarioId",
                table: "ApoiosAdministrativos",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_ProntuarioId_DataRegistro",
                table: "Atendimentos",
                columns: new[] { "ProntuarioId", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_SessaoId",
                table: "Atendimentos",
                column: "SessaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_TratamentoId",
                table: "Atendimentos",
                column: "TratamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_VeterinarioId_DataRegistro",
                table: "Atendimentos",
                columns: new[] { "VeterinarioId", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_ProntuarioId_DataRegistro",
                table: "AvaliacoesClinicas",
                columns: new[] { "ProntuarioId", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_TratamentoId",
                table: "AvaliacoesClinicas",
                column: "TratamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_VeterinarioId_DataRegistro",
                table: "AvaliacoesClinicas",
                columns: new[] { "VeterinarioId", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_BloqueiosAgenda_VeterinarioId_Inicio_Fim",
                table: "BloqueiosAgenda",
                columns: new[] { "VeterinarioId", "Inicio", "Fim" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_EnviadoPorId",
                table: "DocumentosClinicos",
                column: "EnviadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_ProntuarioId",
                table: "DocumentosClinicos",
                column: "ProntuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPrescricao_PrescricaoId",
                table: "ItensPrescricao",
                column: "PrescricaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_DestinatarioId_Lida",
                table: "Mensagens",
                columns: new[] { "DestinatarioId", "Lida" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_PacienteId",
                table: "Mensagens",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_RemetenteId_DestinatarioId_DataEnvio",
                table: "Mensagens",
                columns: new[] { "RemetenteId", "DestinatarioId", "DataEnvio" });

            migrationBuilder.CreateIndex(
                name: "IX_MidiasSessao_AtendimentoId",
                table: "MidiasSessao",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MidiasSessao_SessaoId",
                table: "MidiasSessao",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_UsuarioId_Visualizada_DataCriacao",
                table: "Notificacoes",
                columns: new[] { "UsuarioId", "Visualizada", "DataCriacao" });

            migrationBuilder.CreateIndex(
                name: "IX_ObservacoesInternas_AtendimentoId",
                table: "ObservacoesInternas",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ObservacoesInternas_AutorId",
                table: "ObservacoesInternas",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ObservacoesInternas_AvaliacaoId",
                table: "ObservacoesInternas",
                column: "AvaliacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ObservacoesInternas_ProntuarioId",
                table: "ObservacoesInternas",
                column: "ProntuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pets_ClinicaId_Ativo",
                table: "Pets",
                columns: new[] { "ClinicaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_TutorId",
                table: "Pets",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_PacienteId",
                table: "Prescricoes",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_ProntuarioId_DataEmissao",
                table: "Prescricoes",
                columns: new[] { "ProntuarioId", "DataEmissao" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_VeterinarioId",
                table: "Prescricoes",
                column: "VeterinarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Prontuarios_PacienteId",
                table: "Prontuarios",
                column: "PacienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_ClinicaId_DataHora",
                table: "RegistrosAuditoria",
                columns: new[] { "ClinicaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Entidade_EntidadeId",
                table: "RegistrosAuditoria",
                columns: new[] { "Entidade", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_UsuarioId_DataHora",
                table: "RegistrosAuditoria",
                columns: new[] { "UsuarioId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_TratamentoId_DataHora",
                table: "Sessoes",
                columns: new[] { "TratamentoId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_VeterinarioId_DataHora",
                table: "Sessoes",
                columns: new[] { "VeterinarioId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_TokensRedefinicaoSenha_TokenHash",
                table: "TokensRedefinicaoSenha",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensRedefinicaoSenha_UsuarioId",
                table: "TokensRedefinicaoSenha",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tratamentos_PacienteId_Status",
                table: "Tratamentos",
                columns: new[] { "PacienteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tratamentos_VeterinarioId",
                table: "Tratamentos",
                column: "VeterinarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tutores_UsuarioId",
                table: "Tutores",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ClinicaId_Perfil",
                table: "Usuarios",
                columns: new[] { "ClinicaId", "Perfil" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacinas_PacienteId",
                table: "Vacinas",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacinas_ProximaDose_LembreteEnviado",
                table: "Vacinas",
                columns: new[] { "ProximaDose", "LembreteEnviado" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacinas_VeterinarioId",
                table: "Vacinas",
                column: "VeterinarioId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesRegistrosClinicos_AlteradoPorId",
                table: "VersoesRegistrosClinicos",
                column: "AlteradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesRegistrosClinicos_TipoRegistro_RegistroId",
                table: "VersoesRegistrosClinicos",
                columns: new[] { "TipoRegistro", "RegistroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Veterinarios_Crmv",
                table: "Veterinarios",
                column: "Crmv",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veterinarios_UsuarioId",
                table: "Veterinarios",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlergiasCondicoes");

            migrationBuilder.DropTable(
                name: "ApoiosAdministrativos");

            migrationBuilder.DropTable(
                name: "BloqueiosAgenda");

            migrationBuilder.DropTable(
                name: "DocumentosClinicos");

            migrationBuilder.DropTable(
                name: "ItensPrescricao");

            migrationBuilder.DropTable(
                name: "Mensagens");

            migrationBuilder.DropTable(
                name: "MidiasSessao");

            migrationBuilder.DropTable(
                name: "Notificacoes");

            migrationBuilder.DropTable(
                name: "ObservacoesInternas");

            migrationBuilder.DropTable(
                name: "RegistrosAuditoria");

            migrationBuilder.DropTable(
                name: "TokensRedefinicaoSenha");

            migrationBuilder.DropTable(
                name: "Vacinas");

            migrationBuilder.DropTable(
                name: "VersoesRegistrosClinicos");

            migrationBuilder.DropTable(
                name: "Prescricoes");

            migrationBuilder.DropTable(
                name: "Atendimentos");

            migrationBuilder.DropTable(
                name: "AvaliacoesClinicas");

            migrationBuilder.DropTable(
                name: "Sessoes");

            migrationBuilder.DropTable(
                name: "Prontuarios");

            migrationBuilder.DropTable(
                name: "Tratamentos");

            migrationBuilder.DropTable(
                name: "Pets");

            migrationBuilder.DropTable(
                name: "Veterinarios");

            migrationBuilder.DropTable(
                name: "Tutores");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Clinicas");
        }
    }
}
