using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GerenteEstornoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsuarioGerenteEstornoId",
                table: "Pagamentos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_UsuarioGerenteEstornoId",
                table: "Pagamentos",
                column: "UsuarioGerenteEstornoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Usuarios_UsuarioGerenteEstornoId",
                table: "Pagamentos",
                column: "UsuarioGerenteEstornoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Usuarios_UsuarioGerenteEstornoId",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_UsuarioGerenteEstornoId",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "UsuarioGerenteEstornoId",
                table: "Pagamentos");
        }
    }
}
