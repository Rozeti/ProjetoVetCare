using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <summary>
    /// Cada paciente passa a ter um veterinário responsável: só ele (além da administração)
    /// enxerga o paciente. Os pacientes já cadastrados recebem o veterinário do tratamento
    /// mais recente, que é quem de fato os acompanhava; os que nunca tiveram tratamento
    /// ficam sem responsável até a administração designar alguém pela tela de pacientes.
    /// </summary>
    public partial class VeterinarioResponsavelDoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VeterinarioResponsavelId",
                table: "Pets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pets_VeterinarioResponsavelId",
                table: "Pets",
                column: "VeterinarioResponsavelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pets_Veterinarios_VeterinarioResponsavelId",
                table: "Pets",
                column: "VeterinarioResponsavelId",
                principalTable: "Veterinarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Dados existentes: o veterinário do tratamento mais recente assume o paciente.
            migrationBuilder.Sql("""
                UPDATE "Pets" AS p
                SET "VeterinarioResponsavelId" = ultimo."VeterinarioId"
                FROM (
                    SELECT DISTINCT ON ("PacienteId") "PacienteId", "VeterinarioId"
                    FROM "Tratamentos"
                    ORDER BY "PacienteId", "DataInicio" DESC
                ) AS ultimo
                WHERE ultimo."PacienteId" = p."Id"
                  AND p."VeterinarioResponsavelId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pets_Veterinarios_VeterinarioResponsavelId",
                table: "Pets");

            migrationBuilder.DropIndex(
                name: "IX_Pets_VeterinarioResponsavelId",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "VeterinarioResponsavelId",
                table: "Pets");
        }
    }
}
