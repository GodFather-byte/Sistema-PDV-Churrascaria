using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegrasModificadoresConfiguraveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DestacarObservacoes",
                table: "ItensPedido",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SetorImpressaoId",
                table: "ItensPedido",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DestacarObservacoes",
                table: "GruposModificadores",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SetorImpressaoId",
                table: "GruposModificadores",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SugerePessoas",
                table: "GruposModificadores",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_SetorImpressaoId",
                table: "ItensPedido",
                column: "SetorImpressaoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposModificadores_SetorImpressaoId",
                table: "GruposModificadores",
                column: "SetorImpressaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_GruposModificadores_SetoresImpressao_SetorImpressaoId",
                table: "GruposModificadores",
                column: "SetorImpressaoId",
                principalTable: "SetoresImpressao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_SetoresImpressao_SetorImpressaoId",
                table: "ItensPedido",
                column: "SetorImpressaoId",
                principalTable: "SetoresImpressao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GruposModificadores_SetoresImpressao_SetorImpressaoId",
                table: "GruposModificadores");

            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_SetoresImpressao_SetorImpressaoId",
                table: "ItensPedido");

            migrationBuilder.DropIndex(
                name: "IX_ItensPedido_SetorImpressaoId",
                table: "ItensPedido");

            migrationBuilder.DropIndex(
                name: "IX_GruposModificadores_SetorImpressaoId",
                table: "GruposModificadores");

            migrationBuilder.DropColumn(
                name: "DestacarObservacoes",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "SetorImpressaoId",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "DestacarObservacoes",
                table: "GruposModificadores");

            migrationBuilder.DropColumn(
                name: "SetorImpressaoId",
                table: "GruposModificadores");

            migrationBuilder.DropColumn(
                name: "SugerePessoas",
                table: "GruposModificadores");
        }
    }
}
