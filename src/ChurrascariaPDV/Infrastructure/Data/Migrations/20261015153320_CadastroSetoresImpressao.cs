using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CadastroSetoresImpressao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SetorImpressao",
                table: "Produtos",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "SetoresImpressao",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Porta = table.Column<int>(type: "INTEGER", nullable: false),
                    Habilitada = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecebeContas = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItensEmDestaque = table.Column<bool>(type: "INTEGER", nullable: false),
                    ObservacoesEmDestaque = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfiguracaoLegadaImportada = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetoresImpressao", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SetoresImpressao",
                columns: new[] { "Id", "ConfiguracaoLegadaImportada", "Habilitada", "Ip", "ItensEmDestaque", "Nome", "ObservacoesEmDestaque", "Ordem", "Porta", "RecebeContas" },
                values: new object[,]
                {
                    { "BarCopa", false, true, "192.168.0.101", false, "Bar / Copa", false, 1, 9100, false },
                    { "Caixa", false, true, "192.168.0.100", true, "Caixa", false, 4, 9100, true },
                    { "Churrasqueira", false, true, "192.168.0.103", true, "Churrasqueira", true, 3, 9100, false },
                    { "Cozinha", false, true, "192.168.0.102", true, "Cozinha", false, 2, 9100, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetoresImpressao_Nome",
                table: "SetoresImpressao",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetoresImpressao_RecebeContas",
                table: "SetoresImpressao",
                column: "RecebeContas",
                unique: true,
                filter: "\"RecebeContas\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Produtos SET SetorImpressao = 'Nenhum' WHERE SetorImpressao IS NULL;");

            migrationBuilder.DropTable(
                name: "SetoresImpressao");

            migrationBuilder.AlterColumn<string>(
                name: "SetorImpressao",
                table: "Produtos",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Produtos",
                keyColumn: "Id",
                keyValue: 1,
                column: "SetorImpressao",
                value: "Nenhum");

            migrationBuilder.UpdateData(
                table: "Produtos",
                keyColumn: "Id",
                keyValue: 2,
                column: "SetorImpressao",
                value: "Nenhum");
        }
    }
}
