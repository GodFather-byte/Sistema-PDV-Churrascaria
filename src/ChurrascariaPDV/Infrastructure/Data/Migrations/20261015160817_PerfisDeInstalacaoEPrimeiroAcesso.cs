using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations;

public partial class PerfisDeInstalacaoEPrimeiroAcesso : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // O seed saiu do modelo, mas cadastros existentes não podem ser excluídos.
        migrationBuilder.AddColumn<bool>(
            name: "TrocaPinObrigatoria", table: "Usuarios", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AlterColumn<string>(
            name: "PinAcesso", table: "Usuarios", type: "TEXT", maxLength: 256, nullable: false,
            oldClrType: typeof(string), oldType: "TEXT", oldMaxLength: 4);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TrocaPinObrigatoria", table: "Usuarios");
        // Hashes e cadastros personalizados são preservados no rollback.
    }
}
