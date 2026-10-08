using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaDeDosesERecorrenciaDaVacina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroDose",
                table: "Vacinas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recorrencia",
                table: "Vacinas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Nenhuma");

            migrationBuilder.AddColumn<int>(
                name: "TotalDoses",
                table: "Vacinas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacinas_PacienteId_Nome",
                table: "Vacinas",
                columns: new[] { "PacienteId", "Nome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vacinas_PacienteId_Nome",
                table: "Vacinas");

            migrationBuilder.DropColumn(
                name: "NumeroDose",
                table: "Vacinas");

            migrationBuilder.DropColumn(
                name: "Recorrencia",
                table: "Vacinas");

            migrationBuilder.DropColumn(
                name: "TotalDoses",
                table: "Vacinas");
        }
    }
}
