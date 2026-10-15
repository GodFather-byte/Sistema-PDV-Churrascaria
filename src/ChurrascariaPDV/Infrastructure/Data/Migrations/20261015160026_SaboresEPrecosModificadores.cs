using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SaboresEPrecosModificadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaximoSabores",
                table: "Produtos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "RegraPrecoSabores",
                table: "Produtos",
                type: "TEXT",
                nullable: false,
                defaultValue: "MaiorValor");

            migrationBuilder.AddColumn<decimal>(
                name: "Preco",
                table: "OpcoesModificadores",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Sabores",
                table: "GruposModificadores",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximoSabores",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "RegraPrecoSabores",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Preco",
                table: "OpcoesModificadores");

            migrationBuilder.DropColumn(
                name: "Sabores",
                table: "GruposModificadores");
        }
    }
}
