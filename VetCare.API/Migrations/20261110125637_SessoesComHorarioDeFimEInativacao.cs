using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <summary>
    /// A sessão passa a ter horário de fim, obrigatório no cadastro, e pode ser inativada. As
    /// sessões já existentes terminam depois da duração padrão configurada pela clínica.
    /// </summary>
    public partial class SessoesComHorarioDeFimEInativacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataHoraFim",
                table: "Sessoes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Inativa",
                table: "Sessoes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Até aqui toda sessão durava o tempo padrão da clínica (60 minutos, se não configurado).
            migrationBuilder.Sql("""
                UPDATE "Sessoes" s
                   SET "DataHoraFim" = s."DataHora" + make_interval(mins => COALESCE(
                       (SELECT c."DuracaoSessaoMinutos"
                          FROM "Tratamentos" t
                          JOIN "Pets" p ON p."Id" = t."PacienteId"
                          JOIN "Clinicas" c ON c."Id" = p."ClinicaId"
                         WHERE t."Id" = s."TratamentoId"),
                       60));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataHoraFim",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "Inativa",
                table: "Sessoes");
        }
    }
}
