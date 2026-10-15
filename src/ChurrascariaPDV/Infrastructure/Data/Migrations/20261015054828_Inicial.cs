using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ChurrascariaPDV.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasProdutos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasProdutos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposModificadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Obrigatorio = table.Column<bool>(type: "INTEGER", nullable: false),
                    SelecaoUnica = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposModificadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mesas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    CapacidadeLugares = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DataHoraAbertura = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QuantidadePessoas = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorTotalAtual = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mesas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PinAcesso = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    Perfil = table.Column<string>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produtos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Preco = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoItem = table.Column<string>(type: "TEXT", nullable: false),
                    SetorImpressao = table.Column<string>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produtos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Produtos_CategoriasProdutos_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasProdutos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpcoesModificadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    GrupoModificadorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcoesModificadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcoesModificadores_GruposModificadores_GrupoModificadorId",
                        column: x => x.GrupoModificadorId,
                        principalTable: "GruposModificadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comandas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CodigoNumero = table.Column<int>(type: "INTEGER", nullable: false),
                    MesaId = table.Column<int>(type: "INTEGER", nullable: false),
                    NomeCliente = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CobrarTaxaServico = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataHoraAbertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataHoraFechamento = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comandas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comandas_Mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "Mesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurnosCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataHoraAbertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataHoraFechamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioAberturaId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsuarioFechamentoId = table.Column<int>(type: "INTEGER", nullable: true),
                    FundoTroco = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ValorFechamento = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    ValorEsperadoDinheiro = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnosCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnosCaixa_Usuarios_UsuarioAberturaId",
                        column: x => x.UsuarioAberturaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnosCaixa_Usuarios_UsuarioFechamentoId",
                        column: x => x.UsuarioFechamentoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GruposModificadoresProdutos",
                columns: table => new
                {
                    ProdutoId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoModificadorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposModificadoresProdutos", x => new { x.ProdutoId, x.GrupoModificadorId });
                    table.ForeignKey(
                        name: "FK_GruposModificadoresProdutos_GruposModificadores_GrupoModificadorId",
                        column: x => x.GrupoModificadorId,
                        principalTable: "GruposModificadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GruposModificadoresProdutos_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FechamentosConta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MesaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComandaId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ValorTaxaServico = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    QuantidadePessoas = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosConta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosConta_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosConta_Mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "Mesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MesaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComandaId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioGarcomId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataHoraLancamento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImpressoNosSetores = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "Mesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Usuarios_UsuarioGarcomId",
                        column: x => x.UsuarioGarcomId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TurnoCaixaId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesCaixa_TurnosCaixa_TurnoCaixaId",
                        column: x => x.TurnoCaixaId,
                        principalTable: "TurnosCaixa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimentacoesCaixa_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MesaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComandaId = table.Column<int>(type: "INTEGER", nullable: true),
                    TurnoCaixaId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeioPagamento = table.Column<string>(type: "TEXT", nullable: false),
                    ValorPago = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ValorTroco = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ValorTaxaServicoIncluso = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Estornado = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataHoraEstorno = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoEstorno = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "Mesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_TurnosCaixa_TurnoCaixaId",
                        column: x => x.TurnoCaixaId,
                        principalTable: "TurnosCaixa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProdutoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantidade = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    PrecoUnitarioHistorico = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ObservacoesModificadores = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Cancelado = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    AutorizadoPorGerente = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsuarioGerenteAutorizacaoId = table.Column<int>(type: "INTEGER", nullable: true),
                    DataHoraCancelamento = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Usuarios_UsuarioGerenteAutorizacaoId",
                        column: x => x.UsuarioGerenteAutorizacaoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CategoriasProdutos",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Rodízio" },
                    { 2, "Carnes" },
                    { 3, "Porções" },
                    { 4, "Bebidas" },
                    { 5, "Sobremesas" }
                });

            migrationBuilder.InsertData(
                table: "GruposModificadores",
                columns: new[] { "Id", "Nome", "Obrigatorio", "SelecaoUnica" },
                values: new object[,]
                {
                    { 1, "Ponto da Carne", true, true },
                    { 2, "Preferência Bebida", false, false }
                });

            migrationBuilder.InsertData(
                table: "Mesas",
                columns: new[] { "Id", "CapacidadeLugares", "DataHoraAbertura", "Numero", "QuantidadePessoas", "Status", "ValorTotalAtual" },
                values: new object[,]
                {
                    { 1, 2, null, 1, 0, "Livre", 0m },
                    { 2, 2, null, 2, 0, "Livre", 0m },
                    { 3, 2, null, 3, 0, "Livre", 0m },
                    { 4, 2, null, 4, 0, "Livre", 0m },
                    { 5, 2, null, 5, 0, "Livre", 0m },
                    { 6, 2, null, 6, 0, "Livre", 0m },
                    { 7, 2, null, 7, 0, "Livre", 0m },
                    { 8, 2, null, 8, 0, "Livre", 0m },
                    { 9, 2, null, 9, 0, "Livre", 0m },
                    { 10, 2, null, 10, 0, "Livre", 0m },
                    { 11, 4, null, 11, 0, "Livre", 0m },
                    { 12, 4, null, 12, 0, "Livre", 0m },
                    { 13, 4, null, 13, 0, "Livre", 0m },
                    { 14, 4, null, 14, 0, "Livre", 0m },
                    { 15, 4, null, 15, 0, "Livre", 0m },
                    { 16, 4, null, 16, 0, "Livre", 0m },
                    { 17, 4, null, 17, 0, "Livre", 0m },
                    { 18, 4, null, 18, 0, "Livre", 0m },
                    { 19, 4, null, 19, 0, "Livre", 0m },
                    { 20, 4, null, 20, 0, "Livre", 0m },
                    { 21, 4, null, 21, 0, "Livre", 0m },
                    { 22, 4, null, 22, 0, "Livre", 0m },
                    { 23, 4, null, 23, 0, "Livre", 0m },
                    { 24, 4, null, 24, 0, "Livre", 0m },
                    { 25, 4, null, 25, 0, "Livre", 0m },
                    { 26, 4, null, 26, 0, "Livre", 0m },
                    { 27, 4, null, 27, 0, "Livre", 0m },
                    { 28, 4, null, 28, 0, "Livre", 0m },
                    { 29, 4, null, 29, 0, "Livre", 0m },
                    { 30, 4, null, 30, 0, "Livre", 0m },
                    { 31, 4, null, 31, 0, "Livre", 0m },
                    { 32, 4, null, 32, 0, "Livre", 0m },
                    { 33, 4, null, 33, 0, "Livre", 0m },
                    { 34, 4, null, 34, 0, "Livre", 0m },
                    { 35, 4, null, 35, 0, "Livre", 0m },
                    { 36, 6, null, 36, 0, "Livre", 0m },
                    { 37, 6, null, 37, 0, "Livre", 0m },
                    { 38, 6, null, 38, 0, "Livre", 0m },
                    { 39, 6, null, 39, 0, "Livre", 0m },
                    { 40, 6, null, 40, 0, "Livre", 0m },
                    { 41, 6, null, 41, 0, "Livre", 0m },
                    { 42, 6, null, 42, 0, "Livre", 0m },
                    { 43, 6, null, 43, 0, "Livre", 0m },
                    { 44, 6, null, 44, 0, "Livre", 0m },
                    { 45, 6, null, 45, 0, "Livre", 0m },
                    { 46, 6, null, 46, 0, "Livre", 0m },
                    { 47, 6, null, 47, 0, "Livre", 0m },
                    { 48, 6, null, 48, 0, "Livre", 0m },
                    { 49, 6, null, 49, 0, "Livre", 0m },
                    { 50, 6, null, 50, 0, "Livre", 0m }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Ativo", "Nome", "Perfil", "PinAcesso" },
                values: new object[,]
                {
                    { 1, true, "Gerente", "Gerente", "1234" },
                    { 2, true, "Operador Caixa", "OperadorCaixa", "2222" },
                    { 3, true, "João", "Garcom", "1111" },
                    { 4, true, "Maria", "Garcom", "3333" }
                });

            migrationBuilder.InsertData(
                table: "OpcoesModificadores",
                columns: new[] { "Id", "GrupoModificadorId", "Nome" },
                values: new object[,]
                {
                    { 1, 1, "Malpassada" },
                    { 2, 1, "Ao Ponto" },
                    { 3, 1, "Bem Passada" },
                    { 4, 2, "Com Gelo e Limão" },
                    { 5, 2, "Sem Gelo" },
                    { 6, 2, "2 Copos" }
                });

            migrationBuilder.InsertData(
                table: "Produtos",
                columns: new[] { "Id", "Ativo", "CategoriaId", "Nome", "Preco", "SetorImpressao", "TipoItem" },
                values: new object[,]
                {
                    { 1, true, 1, "Rodízio Completo Adulto", 89.90m, "Nenhum", "Rodizio" },
                    { 2, true, 1, "Rodízio Infantil", 44.90m, "Nenhum", "Rodizio" },
                    { 3, true, 2, "Picanha na Tábua", 119.90m, "Churrasqueira", "CarneAlaCarte" },
                    { 4, true, 2, "Costela no Bafo", 99.90m, "Churrasqueira", "CarneAlaCarte" },
                    { 5, true, 3, "Polenta Frita", 32.90m, "Cozinha", "PorcaoCozinha" },
                    { 6, true, 3, "Mandioca Frita", 34.90m, "Cozinha", "PorcaoCozinha" },
                    { 7, true, 4, "Chopp 500ml", 14.90m, "BarCopa", "Bebida" },
                    { 8, true, 4, "Refrigerante Lata", 8.90m, "BarCopa", "Bebida" },
                    { 9, true, 4, "Suco Jarra", 22.90m, "BarCopa", "Bebida" },
                    { 10, true, 5, "Petit Gateau", 24.90m, "Cozinha", "Sobremesa" }
                });

            migrationBuilder.InsertData(
                table: "GruposModificadoresProdutos",
                columns: new[] { "GrupoModificadorId", "ProdutoId" },
                values: new object[,]
                {
                    { 1, 3 },
                    { 1, 4 },
                    { 2, 7 },
                    { 2, 8 },
                    { 2, 9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProdutos_Nome",
                table: "CategoriasProdutos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comandas_MesaId_CodigoNumero_Status",
                table: "Comandas",
                columns: new[] { "MesaId", "CodigoNumero", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosConta_ComandaId",
                table: "FechamentosConta",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosConta_MesaId",
                table: "FechamentosConta",
                column: "MesaId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposModificadores_Nome",
                table: "GruposModificadores",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposModificadoresProdutos_GrupoModificadorId",
                table: "GruposModificadoresProdutos",
                column: "GrupoModificadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_ProdutoId",
                table: "ItensPedido",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_UsuarioGerenteAutorizacaoId",
                table: "ItensPedido",
                column: "UsuarioGerenteAutorizacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mesas_Numero",
                table: "Mesas",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesCaixa_TurnoCaixaId",
                table: "MovimentacoesCaixa",
                column: "TurnoCaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesCaixa_UsuarioId",
                table: "MovimentacoesCaixa",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcoesModificadores_GrupoModificadorId_Nome",
                table: "OpcoesModificadores",
                columns: new[] { "GrupoModificadorId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_ComandaId",
                table: "Pagamentos",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_MesaId",
                table: "Pagamentos",
                column: "MesaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_TurnoCaixaId",
                table: "Pagamentos",
                column: "TurnoCaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_UsuarioId",
                table: "Pagamentos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ComandaId",
                table: "Pedidos",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_MesaId",
                table: "Pedidos",
                column: "MesaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_UsuarioGarcomId",
                table: "Pedidos",
                column: "UsuarioGarcomId");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_CategoriaId",
                table: "Produtos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnosCaixa_UsuarioAberturaId",
                table: "TurnosCaixa",
                column: "UsuarioAberturaId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnosCaixa_UsuarioFechamentoId",
                table: "TurnosCaixa",
                column: "UsuarioFechamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PinAcesso",
                table: "Usuarios",
                column: "PinAcesso",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FechamentosConta");

            migrationBuilder.DropTable(
                name: "GruposModificadoresProdutos");

            migrationBuilder.DropTable(
                name: "ItensPedido");

            migrationBuilder.DropTable(
                name: "MovimentacoesCaixa");

            migrationBuilder.DropTable(
                name: "OpcoesModificadores");

            migrationBuilder.DropTable(
                name: "Pagamentos");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "Produtos");

            migrationBuilder.DropTable(
                name: "GruposModificadores");

            migrationBuilder.DropTable(
                name: "TurnosCaixa");

            migrationBuilder.DropTable(
                name: "Comandas");

            migrationBuilder.DropTable(
                name: "CategoriasProdutos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Mesas");
        }
    }
}
