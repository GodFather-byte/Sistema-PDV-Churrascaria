using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicencasServidor.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Administradores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SenhaHash = table.Column<string>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administradores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Licencas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Cliente = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Contato = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Observacoes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Maquina = table.Column<string>(type: "TEXT", maxLength: 19, nullable: false),
                    ValidaAte = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Bloqueada = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoBloqueio = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaVerificacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimaVersao = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    UltimoIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    TotalVerificacoes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licencas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Administradores_Usuario",
                table: "Administradores",
                column: "Usuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licencas_Maquina",
                table: "Licencas",
                column: "Maquina");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Administradores");

            migrationBuilder.DropTable(
                name: "Licencas");
        }
    }
}
