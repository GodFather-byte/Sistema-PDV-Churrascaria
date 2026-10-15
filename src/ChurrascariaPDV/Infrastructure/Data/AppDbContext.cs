using ChurrascariaPDV.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<CategoriaProduto> CategoriasProdutos => Set<CategoriaProduto>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<GrupoModificador> GruposModificadores => Set<GrupoModificador>();
    public DbSet<OpcaoModificador> OpcoesModificadores => Set<OpcaoModificador>();
    public DbSet<GrupoModificadorProduto> GruposModificadoresProdutos => Set<GrupoModificadorProduto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<FechamentoConta> FechamentosConta => Set<FechamentoConta>();
    public DbSet<TurnoCaixa> TurnosCaixa => Set<TurnoCaixa>();
    public DbSet<MovimentacaoCaixa> MovimentacoesCaixa => Set<MovimentacaoCaixa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Mesa>(entity =>
        {
            entity.HasIndex(mesa => mesa.Numero).IsUnique();
            entity.Property(mesa => mesa.Status).HasConversion<string>();
            entity.Property(mesa => mesa.ValorTotalAtual).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Comanda>(entity =>
        {
            entity.HasIndex(comanda => new { comanda.MesaId, comanda.CodigoNumero, comanda.Status });
            entity.Property(comanda => comanda.Status).HasConversion<string>();
            entity.Ignore(comanda => comanda.ContaGeral);
            entity.Ignore(comanda => comanda.Descricao);
            entity.Property(comanda => comanda.NomeCliente).HasMaxLength(100);
            entity.HasOne(comanda => comanda.Mesa)
                .WithMany(mesa => mesa.Comandas)
                .HasForeignKey(comanda => comanda.MesaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CategoriaProduto>()
            .HasIndex(categoria => categoria.Nome)
            .IsUnique();

        modelBuilder.Entity<Produto>(entity =>
        {
            entity.Property(produto => produto.Preco).HasPrecision(10, 2);
            entity.Property(produto => produto.TipoItem).HasConversion<string>();
            entity.Property(produto => produto.SetorImpressao).HasConversion<string>();
            entity.HasOne(produto => produto.Categoria)
                .WithMany(categoria => categoria.Produtos)
                .HasForeignKey(produto => produto.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrupoModificador>()
            .HasIndex(grupo => grupo.Nome)
            .IsUnique();

        modelBuilder.Entity<OpcaoModificador>(entity =>
        {
            entity.HasIndex(opcao => new { opcao.GrupoModificadorId, opcao.Nome }).IsUnique();
            entity.Property(opcao => opcao.Nome).HasMaxLength(80);
            entity.HasOne(opcao => opcao.GrupoModificador)
                .WithMany(grupo => grupo.Opcoes)
                .HasForeignKey(opcao => opcao.GrupoModificadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GrupoModificadorProduto>(entity =>
        {
            entity.HasKey(vinculo => new { vinculo.ProdutoId, vinculo.GrupoModificadorId });
            entity.HasOne(vinculo => vinculo.Produto)
                .WithMany(produto => produto.GruposModificadores)
                .HasForeignKey(vinculo => vinculo.ProdutoId);
            entity.HasOne(vinculo => vinculo.GrupoModificador)
                .WithMany(grupo => grupo.Produtos)
                .HasForeignKey(vinculo => vinculo.GrupoModificadorId);
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.Property(pedido => pedido.Status).HasConversion<string>();
            entity.HasOne(pedido => pedido.Mesa)
                .WithMany(mesa => mesa.Pedidos)
                .HasForeignKey(pedido => pedido.MesaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pedido => pedido.Comanda)
                .WithMany(comanda => comanda.Pedidos)
                .HasForeignKey(pedido => pedido.ComandaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pedido => pedido.UsuarioGarcom)
                .WithMany(usuario => usuario.Pedidos)
                .HasForeignKey(pedido => pedido.UsuarioGarcomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemPedido>(entity =>
        {
            entity.Property(item => item.Quantidade).HasPrecision(10, 3);
            entity.Property(item => item.PrecoUnitarioHistorico).HasPrecision(10, 2);
            entity.Property(item => item.ObservacoesModificadores).HasMaxLength(500);
            entity.Property(item => item.MotivoCancelamento).HasMaxLength(250);
            entity.Ignore(item => item.ValorTotal);
            entity.HasOne(item => item.Pedido)
                .WithMany(pedido => pedido.Itens)
                .HasForeignKey(item => item.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Produto)
                .WithMany(produto => produto.ItensPedido)
                .HasForeignKey(item => item.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.UsuarioGerenteAutorizacao)
                .WithMany()
                .HasForeignKey(item => item.UsuarioGerenteAutorizacaoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pagamento>(entity =>
        {
            entity.Property(pagamento => pagamento.MeioPagamento).HasConversion<string>();
            entity.Property(pagamento => pagamento.ValorPago).HasPrecision(10, 2);
            entity.Property(pagamento => pagamento.ValorTroco).HasPrecision(10, 2);
            entity.Property(pagamento => pagamento.ValorTaxaServicoIncluso).HasPrecision(10, 2);
            entity.Property(pagamento => pagamento.MotivoEstorno).HasMaxLength(250);
            entity.Ignore(pagamento => pagamento.ValorRecebido);
            entity.HasOne(pagamento => pagamento.TurnoCaixa)
                .WithMany(turno => turno.Pagamentos)
                .HasForeignKey(pagamento => pagamento.TurnoCaixaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pagamento => pagamento.Usuario)
                .WithMany()
                .HasForeignKey(pagamento => pagamento.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pagamento => pagamento.Mesa)
                .WithMany(mesa => mesa.Pagamentos)
                .HasForeignKey(pagamento => pagamento.MesaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pagamento => pagamento.Comanda)
                .WithMany(comanda => comanda.Pagamentos)
                .HasForeignKey(pagamento => pagamento.ComandaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FechamentoConta>(entity =>
        {
            entity.Property(fechamento => fechamento.Subtotal).HasPrecision(10, 2);
            entity.Property(fechamento => fechamento.ValorTaxaServico).HasPrecision(10, 2);
            entity.Property(fechamento => fechamento.Total).HasPrecision(10, 2);
            entity.HasOne(fechamento => fechamento.Mesa)
                .WithMany()
                .HasForeignKey(fechamento => fechamento.MesaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(fechamento => fechamento.Comanda)
                .WithMany()
                .HasForeignKey(fechamento => fechamento.ComandaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TurnoCaixa>(entity =>
        {
            entity.Property(turno => turno.FundoTroco).HasPrecision(10, 2);
            entity.Property(turno => turno.ValorFechamento).HasPrecision(10, 2);
            entity.Property(turno => turno.ValorEsperadoDinheiro).HasPrecision(10, 2);
            entity.Property(turno => turno.Observacao).HasMaxLength(250);
            entity.Ignore(turno => turno.Aberto);
            entity.HasOne(turno => turno.UsuarioAbertura)
                .WithMany()
                .HasForeignKey(turno => turno.UsuarioAberturaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(turno => turno.UsuarioFechamento)
                .WithMany()
                .HasForeignKey(turno => turno.UsuarioFechamentoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentacaoCaixa>(entity =>
        {
            entity.Property(movimentacao => movimentacao.Tipo).HasConversion<string>();
            entity.Property(movimentacao => movimentacao.Valor).HasPrecision(10, 2);
            entity.Property(movimentacao => movimentacao.Observacao).HasMaxLength(250);
            entity.HasOne(movimentacao => movimentacao.TurnoCaixa)
                .WithMany(turno => turno.Movimentacoes)
                .HasForeignKey(movimentacao => movimentacao.TurnoCaixaId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(movimentacao => movimentacao.Usuario)
                .WithMany()
                .HasForeignKey(movimentacao => movimentacao.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.Property(usuario => usuario.Nome).HasMaxLength(100).IsRequired();
            entity.Property(usuario => usuario.PinAcesso).HasMaxLength(4).IsRequired();
            entity.Property(usuario => usuario.Perfil).HasConversion<string>();
            entity.HasIndex(usuario => usuario.PinAcesso).IsUnique();
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mesa>().HasData(
            Enumerable.Range(1, 50).Select(numero => new Mesa
            {
                Id = numero,
                Numero = numero,
                CapacidadeLugares = numero <= 10 ? 2 : numero <= 35 ? 4 : 6,
                Status = Domain.Enums.StatusMesa.Livre,
                QuantidadePessoas = 0,
                ValorTotalAtual = 0
            }));

        modelBuilder.Entity<Usuario>().HasData(
            new Usuario { Id = 1, Nome = "Gerente", PinAcesso = "1234", Perfil = Domain.Enums.PerfilUsuario.Gerente, Ativo = true },
            new Usuario { Id = 2, Nome = "Operador Caixa", PinAcesso = "2222", Perfil = Domain.Enums.PerfilUsuario.OperadorCaixa, Ativo = true },
            new Usuario { Id = 3, Nome = "João", PinAcesso = "1111", Perfil = Domain.Enums.PerfilUsuario.Garcom, Ativo = true },
            new Usuario { Id = 4, Nome = "Maria", PinAcesso = "3333", Perfil = Domain.Enums.PerfilUsuario.Garcom, Ativo = true });

        modelBuilder.Entity<CategoriaProduto>().HasData(
            new CategoriaProduto { Id = 1, Nome = "Rodízio" },
            new CategoriaProduto { Id = 2, Nome = "Carnes" },
            new CategoriaProduto { Id = 3, Nome = "Porções" },
            new CategoriaProduto { Id = 4, Nome = "Bebidas" },
            new CategoriaProduto { Id = 5, Nome = "Sobremesas" });

        modelBuilder.Entity<Produto>().HasData(
            new { Id = 1, Nome = "Rodízio Completo Adulto", Preco = 89.90m, CategoriaId = 1, TipoItem = Domain.Enums.TipoItem.Rodizio, SetorImpressao = Domain.Enums.SetorImpressao.Nenhum, Ativo = true },
            new { Id = 2, Nome = "Rodízio Infantil", Preco = 44.90m, CategoriaId = 1, TipoItem = Domain.Enums.TipoItem.Rodizio, SetorImpressao = Domain.Enums.SetorImpressao.Nenhum, Ativo = true },
            new { Id = 3, Nome = "Picanha na Tábua", Preco = 119.90m, CategoriaId = 2, TipoItem = Domain.Enums.TipoItem.CarneAlaCarte, SetorImpressao = Domain.Enums.SetorImpressao.Churrasqueira, Ativo = true },
            new { Id = 4, Nome = "Costela no Bafo", Preco = 99.90m, CategoriaId = 2, TipoItem = Domain.Enums.TipoItem.CarneAlaCarte, SetorImpressao = Domain.Enums.SetorImpressao.Churrasqueira, Ativo = true },
            new { Id = 5, Nome = "Polenta Frita", Preco = 32.90m, CategoriaId = 3, TipoItem = Domain.Enums.TipoItem.PorcaoCozinha, SetorImpressao = Domain.Enums.SetorImpressao.Cozinha, Ativo = true },
            new { Id = 6, Nome = "Mandioca Frita", Preco = 34.90m, CategoriaId = 3, TipoItem = Domain.Enums.TipoItem.PorcaoCozinha, SetorImpressao = Domain.Enums.SetorImpressao.Cozinha, Ativo = true },
            new { Id = 7, Nome = "Chopp 500ml", Preco = 14.90m, CategoriaId = 4, TipoItem = Domain.Enums.TipoItem.Bebida, SetorImpressao = Domain.Enums.SetorImpressao.BarCopa, Ativo = true },
            new { Id = 8, Nome = "Refrigerante Lata", Preco = 8.90m, CategoriaId = 4, TipoItem = Domain.Enums.TipoItem.Bebida, SetorImpressao = Domain.Enums.SetorImpressao.BarCopa, Ativo = true },
            new { Id = 9, Nome = "Suco Jarra", Preco = 22.90m, CategoriaId = 4, TipoItem = Domain.Enums.TipoItem.Bebida, SetorImpressao = Domain.Enums.SetorImpressao.BarCopa, Ativo = true },
            new { Id = 10, Nome = "Petit Gateau", Preco = 24.90m, CategoriaId = 5, TipoItem = Domain.Enums.TipoItem.Sobremesa, SetorImpressao = Domain.Enums.SetorImpressao.Cozinha, Ativo = true });

        modelBuilder.Entity<GrupoModificador>().HasData(
            new GrupoModificador { Id = 1, Nome = "Ponto da Carne", Obrigatorio = true, SelecaoUnica = true },
            new GrupoModificador { Id = 2, Nome = "Preferência Bebida", Obrigatorio = false, SelecaoUnica = false });

        modelBuilder.Entity<OpcaoModificador>().HasData(
            new OpcaoModificador { Id = 1, GrupoModificadorId = 1, Nome = "Malpassada" },
            new OpcaoModificador { Id = 2, GrupoModificadorId = 1, Nome = "Ao Ponto" },
            new OpcaoModificador { Id = 3, GrupoModificadorId = 1, Nome = "Bem Passada" },
            new OpcaoModificador { Id = 4, GrupoModificadorId = 2, Nome = "Com Gelo e Limão" },
            new OpcaoModificador { Id = 5, GrupoModificadorId = 2, Nome = "Sem Gelo" },
            new OpcaoModificador { Id = 6, GrupoModificadorId = 2, Nome = "2 Copos" });

        modelBuilder.Entity<GrupoModificadorProduto>().HasData(
            new GrupoModificadorProduto { ProdutoId = 3, GrupoModificadorId = 1 },
            new GrupoModificadorProduto { ProdutoId = 4, GrupoModificadorId = 1 },
            new GrupoModificadorProduto { ProdutoId = 7, GrupoModificadorId = 2 },
            new GrupoModificadorProduto { ProdutoId = 8, GrupoModificadorId = 2 },
            new GrupoModificadorProduto { ProdutoId = 9, GrupoModificadorId = 2 });
    }
}
