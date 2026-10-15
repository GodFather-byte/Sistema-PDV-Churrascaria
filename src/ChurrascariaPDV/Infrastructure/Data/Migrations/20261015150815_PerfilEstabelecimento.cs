using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerfilEstabelecimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerfisEstabelecimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Endereco = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    PercentualTaxaServico = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    TaxaServicoOpcionalPorComanda = table.Column<bool>(type: "INTEGER", nullable: false),
                    LarguraTicket = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 48)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisEstabelecimento", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PerfisEstabelecimento",
                columns: new[] { "Id", "Endereco", "LarguraTicket", "Nome", "PercentualTaxaServico", "TaxaServicoOpcionalPorComanda", "Tipo" },
                values: new object[] { 1, "", 48, "CHURRASCARIA", 10m, true, "Churrascaria" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerfisEstabelecimento");
        }
    }
}
