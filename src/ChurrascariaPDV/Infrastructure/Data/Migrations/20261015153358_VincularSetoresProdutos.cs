using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VincularSetoresProdutos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Produtos SET SetorImpressao = NULL WHERE SetorImpressao = 'Nenhum';");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_SetorImpressao",
                table: "Produtos",
                column: "SetorImpressao");

            migrationBuilder.AddForeignKey(
                name: "FK_Produtos_SetoresImpressao_SetorImpressao",
                table: "Produtos",
                column: "SetorImpressao",
                principalTable: "SetoresImpressao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Produtos_SetoresImpressao_SetorImpressao",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_SetorImpressao",
                table: "Produtos");
        }
    }
}
