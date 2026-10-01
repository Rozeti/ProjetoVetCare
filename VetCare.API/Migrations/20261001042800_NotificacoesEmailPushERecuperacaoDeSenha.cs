using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetCare.API.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesEmailPushERecuperacaoDeSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificarPorEmail",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarPorPush",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CodigoHash",
                table: "TokensRedefinicaoSenha",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Finalidade",
                table: "TokensRedefinicaoSenha",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TentativasDeCodigo",
                table: "TokensRedefinicaoSenha",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailEnviadoEm",
                table: "Notificacoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErroDeEntrega",
                table: "Notificacoes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximaTentativaEm",
                table: "Notificacoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PushEnviadoEm",
                table: "Notificacoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoEntrega",
                table: "Notificacoes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TentativasDeEntrega",
                table: "Notificacoes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // As notificações que já existiam foram vistas no sistema; não há o que reenviar.
            migrationBuilder.Sql("UPDATE \"Notificacoes\" SET \"SituacaoEntrega\" = 'Entregue' WHERE \"SituacaoEntrega\" = '';");

            migrationBuilder.CreateTable(
                name: "DispositivosDoUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenPush = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Plataforma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NomeDoAparelho = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoUsoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositivosDoUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositivosDoUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_SituacaoEntrega_ProximaTentativaEm",
                table: "Notificacoes",
                columns: new[] { "SituacaoEntrega", "ProximaTentativaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosDoUsuario_TokenPush",
                table: "DispositivosDoUsuario",
                column: "TokenPush",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosDoUsuario_UsuarioId_Ativo",
                table: "DispositivosDoUsuario",
                columns: new[] { "UsuarioId", "Ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispositivosDoUsuario");

            migrationBuilder.DropIndex(
                name: "IX_Notificacoes_SituacaoEntrega_ProximaTentativaEm",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "NotificarPorEmail",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NotificarPorPush",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoHash",
                table: "TokensRedefinicaoSenha");

            migrationBuilder.DropColumn(
                name: "Finalidade",
                table: "TokensRedefinicaoSenha");

            migrationBuilder.DropColumn(
                name: "TentativasDeCodigo",
                table: "TokensRedefinicaoSenha");

            migrationBuilder.DropColumn(
                name: "EmailEnviadoEm",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "ErroDeEntrega",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "ProximaTentativaEm",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "PushEnviadoEm",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "SituacaoEntrega",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "TentativasDeEntrega",
                table: "Notificacoes");
        }
    }
}
