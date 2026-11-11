using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <summary>
    /// Receitas e pedidos de exame passam a ter duas origens: a avaliação ou o tratamento (direto
    /// nele ou no registro de uma das sessões dele). O pedido de exame deixa de exigir uma
    /// avaliação: no pedido feito no tratamento ou em uma sessão, a avaliação fica vazia. Os
    /// pedidos já feitos em sessões, que guardavam a avaliação do tratamento, passam a não
    /// guardá-la; o Down a devolve a partir do tratamento. O modelo anterior não distingue o
    /// pedido feito no tratamento do pedido da avaliação ligado a ele: depois de um Down, o
    /// primeiro passa a ser lido como o segundo.
    /// </summary>
    public partial class OrigemDeReceitasEPedidosNoTratamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidosExame_AvaliacoesClinicas_AvaliacaoId",
                table: "PedidosExame");

            migrationBuilder.AlterColumn<Guid>(
                name: "AvaliacaoId",
                table: "PedidosExame",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosExame_AvaliacoesClinicas_AvaliacaoId",
                table: "PedidosExame",
                column: "AvaliacaoId",
                principalTable: "AvaliacoesClinicas",
                principalColumn: "Id");

            // O pedido feito em uma sessão é do tratamento: a avaliação só fica no pedido feito nela.
            migrationBuilder.Sql("""
                UPDATE "PedidosExame"
                SET "AvaliacaoId" = NULL
                WHERE "AtendimentoId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta ao modelo anterior, em que todo pedido guardava uma avaliação: a do tratamento
            // do pedido (ou do tratamento da sessão em que ele foi feito).
            migrationBuilder.Sql("""
                UPDATE "PedidosExame" AS p
                SET "AvaliacaoId" = t."AvaliacaoId"
                FROM "Tratamentos" AS t
                WHERE p."AvaliacaoId" IS NULL
                  AND t."Id" = COALESCE(
                      p."TratamentoId",
                      (SELECT a."TratamentoId" FROM "Atendimentos" AS a WHERE a."Id" = p."AtendimentoId"));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosExame_AvaliacoesClinicas_AvaliacaoId",
                table: "PedidosExame");

            migrationBuilder.AlterColumn<Guid>(
                name: "AvaliacaoId",
                table: "PedidosExame",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosExame_AvaliacoesClinicas_AvaliacaoId",
                table: "PedidosExame",
                column: "AvaliacaoId",
                principalTable: "AvaliacoesClinicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
