using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <summary>
    /// Reorganiza o fluxo clínico em Animal → Avaliações → Tratamentos → Evoluções.
    ///
    /// A avaliação deixa de pertencer a um tratamento e passa a pertencer ao animal; o
    /// tratamento passa a exigir a avaliação de origem. Os dados existentes são convertidos,
    /// não descartados:
    /// - observações da avaliação = anamnese + exame físico + plano terapêutico;
    /// - origem de cada tratamento = a primeira avaliação que já estava ligada a ele; os
    ///   tratamentos sem avaliação ganham uma avaliação de origem registrada pela migração;
    /// - descrição e nome do tratamento = o antigo objetivo terapêutico;
    /// - evoluções numeradas por tratamento, na ordem em que foram registradas;
    /// - "Interrompido" passa a se chamar "Inativado".
    /// </summary>
    public partial class FluxoClinicoAvaliacaoTratamentoEvolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------ avaliações
            migrationBuilder.AddColumn<Guid>(
                name: "PacienteId",
                table: "AvaliacoesClinicas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "AvaliacoesClinicas",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "AvaliacoesClinicas" a
                   SET "PacienteId" = p."PacienteId"
                  FROM "Prontuarios" p
                 WHERE p."Id" = a."ProntuarioId";

                UPDATE "AvaliacoesClinicas"
                   SET "Observacoes" = LEFT(COALESCE(NULLIF(CONCAT_WS(E'\n\n',
                           CASE WHEN BTRIM("Anamnese") <> '' THEN 'Anamnese: ' || "Anamnese" END,
                           CASE WHEN BTRIM("ExameFisico") <> '' THEN 'Exame físico: ' || "ExameFisico" END,
                           CASE WHEN BTRIM("PlanoTerapeutico") <> '' THEN 'Plano terapêutico: ' || "PlanoTerapeutico" END),
                       ''), 'Sem observações registradas.'), 8000);
                """);

            // ------------------------------------------------------------ tratamentos
            migrationBuilder.AddColumn<Guid>(
                name: "AvaliacaoId",
                table: "Tratamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "Tratamentos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Tratamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Tratamentos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DuracaoPrevista",
                table: "Tratamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Frequencia",
                table: "Tratamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SessoesPrevistas",
                table: "Tratamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Tratamentos" t
                   SET "Descricao" = LEFT("ObjetivoTerapeutico", 2000),
                       "Nome" = CASE WHEN CHAR_LENGTH(BTRIM("ObjetivoTerapeutico")) <= 120
                                     THEN BTRIM("ObjetivoTerapeutico")
                                     ELSE LEFT(BTRIM("ObjetivoTerapeutico"), 117) || '...' END,
                       "Tipo" = 'Fisioterapia',
                       "DuracaoPrevista" = 'Não informada',
                       "Frequencia" = 'Não informada',
                       "SessoesPrevistas" = GREATEST(1, (SELECT COUNT(*) FROM "Sessoes" s
                                                          WHERE s."TratamentoId" = t."Id" AND s."Status" <> 'Cancelada')),
                       "Status" = CASE WHEN "Status" = 'Interrompido' THEN 'Inativado' ELSE "Status" END;

                -- A origem é a primeira avaliação que já estava ligada ao tratamento.
                UPDATE "Tratamentos" t
                   SET "AvaliacaoId" = (SELECT a."Id" FROM "AvaliacoesClinicas" a
                                         WHERE a."TratamentoId" = t."Id"
                                         ORDER BY a."DataRegistro", a."Id"
                                         LIMIT 1);

                -- Tratamentos sem nenhuma avaliação ganham uma avaliação de origem registrada
                -- pela migração, no prontuário do paciente (criado se ainda não existir).
                INSERT INTO "Prontuarios" ("Id", "PacienteId", "DataCriacao", "UltimaAtualizacao")
                SELECT gen_random_uuid(), sem_origem."PacienteId", NOW(), NOW()
                  FROM (SELECT DISTINCT "PacienteId" FROM "Tratamentos" WHERE "AvaliacaoId" IS NULL) sem_origem
                 WHERE NOT EXISTS (SELECT 1 FROM "Prontuarios" p WHERE p."PacienteId" = sem_origem."PacienteId");

                WITH novas AS (
                    INSERT INTO "AvaliacoesClinicas"
                        ("Id", "TratamentoId", "PacienteId", "ProntuarioId", "VeterinarioId",
                         "QueixaPrincipal", "Anamnese", "ExameFisico", "HipoteseDiagnostica", "PlanoTerapeutico",
                         "Observacoes", "DataRegistro")
                    SELECT gen_random_uuid(), t."Id", t."PacienteId", p."Id", t."VeterinarioId",
                           'Avaliação de origem registrada na reorganização do fluxo clínico', '', '', 'Não informado', '',
                           LEFT('Este tratamento foi aberto antes de o sistema exigir uma avaliação de origem. ' ||
                                'Objetivo terapêutico registrado: ' || t."ObjetivoTerapeutico", 8000),
                           t."DataInicio"
                      FROM "Tratamentos" t
                      JOIN "Prontuarios" p ON p."PacienteId" = t."PacienteId"
                     WHERE t."AvaliacaoId" IS NULL
                    RETURNING "Id", "TratamentoId"
                )
                UPDATE "Tratamentos" t
                   SET "AvaliacaoId" = novas."Id"
                  FROM novas
                 WHERE novas."TratamentoId" = t."Id";
                """);

            // ------------------------------------------------------------ evoluções
            migrationBuilder.AddColumn<int>(
                name: "NumeroSessao",
                table: "Atendimentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReacaoAnimal",
                table: "Atendimentos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "Atendimentos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Relatorio",
                table: "Atendimentos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Atendimentos" a
                   SET "NumeroSessao" = numeradas.numero,
                       "ReacaoAnimal" = 'Não registrada.'
                  FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "TratamentoId" ORDER BY "DataRegistro", "Id") AS numero
                          FROM "Atendimentos") numeradas
                 WHERE numeradas."Id" = a."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Atendimentos_TratamentoId",
                table: "Atendimentos");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_TratamentoId_NumeroSessao",
                table: "Atendimentos",
                columns: new[] { "TratamentoId", "NumeroSessao" });

            // ------------------------------------------------------------ receitas
            migrationBuilder.AddColumn<Guid>(
                name: "AvaliacaoId",
                table: "Prescricoes",
                type: "uuid",
                nullable: true);

            // A referência à evolução passa a ter chave estrangeira: um valor órfão vira "sem origem".
            migrationBuilder.Sql("""
                UPDATE "Prescricoes"
                   SET "AtendimentoId" = NULL
                 WHERE "AtendimentoId" IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM "Atendimentos" a WHERE a."Id" = "Prescricoes"."AtendimentoId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_AtendimentoId",
                table: "Prescricoes",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_AvaliacaoId",
                table: "Prescricoes",
                column: "AvaliacaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescricoes_Atendimentos_AtendimentoId",
                table: "Prescricoes",
                column: "AtendimentoId",
                principalTable: "Atendimentos",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescricoes_AvaliacoesClinicas_AvaliacaoId",
                table: "Prescricoes",
                column: "AvaliacaoId",
                principalTable: "AvaliacoesClinicas",
                principalColumn: "Id");

            // ------------------------------------------------------------ colunas antigas
            migrationBuilder.DropForeignKey(
                name: "FK_AvaliacoesClinicas_Tratamentos_TratamentoId",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropIndex(
                name: "IX_AvaliacoesClinicas_TratamentoId",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "TratamentoId",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "Anamnese",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "ExameFisico",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "PlanoTerapeutico",
                table: "AvaliacoesClinicas");

            migrationBuilder.RenameColumn(
                name: "QueixaPrincipal",
                table: "AvaliacoesClinicas",
                newName: "MotivoConsulta");

            migrationBuilder.RenameColumn(
                name: "HipoteseDiagnostica",
                table: "AvaliacoesClinicas",
                newName: "DiagnosticoPossivel");

            migrationBuilder.DropColumn(
                name: "ObjetivoTerapeutico",
                table: "Tratamentos");

            // ------------------------------------------------------------ novas restrições
            migrationBuilder.AlterColumn<Guid>(
                name: "PacienteId",
                table: "AvaliacoesClinicas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_PacienteId",
                table: "AvaliacoesClinicas",
                column: "PacienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_AvaliacoesClinicas_Pets_PacienteId",
                table: "AvaliacoesClinicas",
                column: "PacienteId",
                principalTable: "Pets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AlterColumn<Guid>(
                name: "AvaliacaoId",
                table: "Tratamentos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tratamentos_AvaliacaoId",
                table: "Tratamentos",
                column: "AvaliacaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tratamentos_AvaliacoesClinicas_AvaliacaoId",
                table: "Tratamentos",
                column: "AvaliacaoId",
                principalTable: "AvaliacoesClinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ------------------------------------------------------------ pedidos de exame
            migrationBuilder.CreateTable(
                name: "PedidosExame",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProntuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvaliacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Exames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosExame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosExame_AvaliacoesClinicas_AvaliacaoId",
                        column: x => x.AvaliacaoId,
                        principalTable: "AvaliacoesClinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosExame_Pets_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosExame_Prontuarios_ProntuarioId",
                        column: x => x.ProntuarioId,
                        principalTable: "Prontuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosExame_Veterinarios_VeterinarioId",
                        column: x => x.VeterinarioId,
                        principalTable: "Veterinarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_AvaliacaoId",
                table: "PedidosExame",
                column: "AvaliacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_PacienteId",
                table: "PedidosExame",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_ProntuarioId_DataEmissao",
                table: "PedidosExame",
                columns: new[] { "ProntuarioId", "DataEmissao" });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_VeterinarioId",
                table: "PedidosExame",
                column: "VeterinarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // As avaliações deixaram de pertencer a um tratamento e os campos antigos foram
            // reunidos em "observações": não há como devolver cada dado à coluna de origem sem
            // inventar valores. Para voltar à versão anterior, restaure um backup do banco.
            throw new NotSupportedException(
                "A reorganização do fluxo clínico não pode ser desfeita automaticamente. " +
                "Restaure um backup do banco feito antes da migração FluxoClinicoAvaliacaoTratamentoEvolucao.");
        }
    }
}
