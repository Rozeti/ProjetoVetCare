using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <summary>
    /// Receitas e pedidos de exame passam a guardar o tratamento para o qual foram feitos, e o
    /// pedido de exame pode nascer de uma sessão registrada. Os registros já existentes recebem o
    /// tratamento quando ele é certo: o da sessão de origem ou, na origem avaliação, o único
    /// tratamento que a avaliação originou. Os demais ficam sem vínculo.
    /// </summary>
    public partial class VinculoDeReceitasEPedidosAoTratamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TratamentoId",
                table: "Prescricoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AtendimentoId",
                table: "PedidosExame",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TratamentoId",
                table: "PedidosExame",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescricoes_TratamentoId",
                table: "Prescricoes",
                column: "TratamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_AtendimentoId",
                table: "PedidosExame",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExame_TratamentoId",
                table: "PedidosExame",
                column: "TratamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosExame_Atendimentos_AtendimentoId",
                table: "PedidosExame",
                column: "AtendimentoId",
                principalTable: "Atendimentos",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosExame_Tratamentos_TratamentoId",
                table: "PedidosExame",
                column: "TratamentoId",
                principalTable: "Tratamentos",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescricoes_Tratamentos_TratamentoId",
                table: "Prescricoes",
                column: "TratamentoId",
                principalTable: "Tratamentos",
                principalColumn: "Id");

            // Receita de sessão: o tratamento é o da própria sessão.
            migrationBuilder.Sql("""
                UPDATE "Prescricoes" AS p
                SET "TratamentoId" = a."TratamentoId"
                FROM "Atendimentos" AS a
                WHERE p."AtendimentoId" = a."Id" AND p."TratamentoId" IS NULL;
                """);

            // Receita e pedido de avaliação: só quando a avaliação originou um único tratamento.
            migrationBuilder.Sql("""
                UPDATE "Prescricoes" AS p
                SET "TratamentoId" = t."Id"
                FROM "Tratamentos" AS t
                WHERE p."AvaliacaoId" = t."AvaliacaoId"
                  AND p."TratamentoId" IS NULL
                  AND (SELECT COUNT(*) FROM "Tratamentos" AS u WHERE u."AvaliacaoId" = p."AvaliacaoId") = 1;
                """);

            migrationBuilder.Sql("""
                UPDATE "PedidosExame" AS p
                SET "TratamentoId" = t."Id"
                FROM "Tratamentos" AS t
                WHERE p."AvaliacaoId" = t."AvaliacaoId"
                  AND p."TratamentoId" IS NULL
                  AND (SELECT COUNT(*) FROM "Tratamentos" AS u WHERE u."AvaliacaoId" = p."AvaliacaoId") = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidosExame_Atendimentos_AtendimentoId",
                table: "PedidosExame");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosExame_Tratamentos_TratamentoId",
                table: "PedidosExame");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescricoes_Tratamentos_TratamentoId",
                table: "Prescricoes");

            migrationBuilder.DropIndex(
                name: "IX_Prescricoes_TratamentoId",
                table: "Prescricoes");

            migrationBuilder.DropIndex(
                name: "IX_PedidosExame_AtendimentoId",
                table: "PedidosExame");

            migrationBuilder.DropIndex(
                name: "IX_PedidosExame_TratamentoId",
                table: "PedidosExame");

            migrationBuilder.DropColumn(
                name: "TratamentoId",
                table: "Prescricoes");

            migrationBuilder.DropColumn(
                name: "AtendimentoId",
                table: "PedidosExame");

            migrationBuilder.DropColumn(
                name: "TratamentoId",
                table: "PedidosExame");
        }
    }
}
