using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreservarRegrasPedidosLegados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE ItensPedido SET
                    SetorImpressaoId = (SELECT SetorImpressao FROM Produtos WHERE Produtos.Id = ItensPedido.ProdutoId),
                    DestacarObservacoes = COALESCE((
                        SELECT s.ObservacoesEmDestaque FROM Produtos p
                        JOIN SetoresImpressao s ON s.Id = p.SetorImpressao
                        WHERE p.Id = ItensPedido.ProdutoId), 0);
                """);

            // IDs e nomes livres preservam os grupos criados antes da atualizacao.
            migrationBuilder.Sql("""
                WITH RECURSIVE Candidatos(Numero, Nome) AS (
                    SELECT 1, 'Consumo por pessoa'
                    UNION ALL
                    SELECT Numero + 1, 'Consumo por pessoa ' || (Numero + 1)
                    FROM Candidatos WHERE Numero <= (SELECT COUNT(*) FROM GruposModificadores)
                )
                INSERT INTO GruposModificadores
                    (Id, Nome, Obrigatorio, SelecaoUnica, DestacarObservacoes, SugerePessoas, SetorImpressaoId)
                SELECT (SELECT COALESCE(MAX(Id), 0) + 1 FROM GruposModificadores), Nome, 0, 0, 0, 1, NULL
                FROM Candidatos WHERE NOT EXISTS (SELECT 1 FROM GruposModificadores g WHERE g.Nome = Candidatos.Nome)
                ORDER BY Numero LIMIT 1;

                INSERT INTO GruposModificadoresProdutos (ProdutoId, GrupoModificadorId)
                SELECT Id, (SELECT MAX(Id) FROM GruposModificadores) FROM Produtos WHERE TipoItem = 'Rodizio';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // As configuracoes e vinculos sao preservados no rollback para nao apagar edicoes do administrador.
        }
    }
}
