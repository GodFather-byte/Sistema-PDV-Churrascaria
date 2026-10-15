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
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PerfilEstabelecimento> PerfisEstabelecimento => Set<PerfilEstabelecimento>();
    public DbSet<SetorImpressao> SetoresImpressao => Set<SetorImpressao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SetorImpressao>(entity =>
        {
            entity.Property(setor => setor.Id).HasMaxLength(32);
            entity.Property(setor => setor.Nome).HasMaxLength(80).IsRequired();
            entity.Property(setor => setor.Ip).HasMaxLength(45).IsRequired();
            entity.HasIndex(setor => setor.Nome).IsUnique();
            entity.HasIndex(setor => setor.RecebeContas).IsUnique().HasFilter("\"RecebeContas\" = 1");
        });

        modelBuilder.Entity<PerfilEstabelecimento>(entity =>
        {
            entity.Property(perfil => perfil.Tipo).HasConversion<string>();
            entity.Property(perfil => perfil.Nome).HasMaxLength(120).IsRequired();
            entity.Property(perfil => perfil.Endereco).HasMaxLength(250).IsRequired();
            entity.Property(perfil => perfil.PercentualTaxaServico).HasPrecision(5, 2);
            entity.Property(perfil => perfil.LarguraTicket).HasDefaultValue(48);
        });

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
            entity.Property(produto => produto.RegraPrecoSabores).HasConversion<string>().HasDefaultValue(Domain.Enums.RegraPrecoSabores.MaiorValor);
            entity.Property(produto => produto.MaximoSabores).HasDefaultValue(2);
            entity.Property(produto => produto.QuantidadeEstoque).HasPrecision(10, 3);
            entity.Property(produto => produto.TipoItem).HasConversion<string>();
            entity.Property(produto => produto.SetorImpressaoId).HasColumnName("SetorImpressao");
            entity.HasOne(produto => produto.SetorImpressao).WithMany()
                .HasForeignKey(produto => produto.SetorImpressaoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(produto => produto.Categoria)
                .WithMany(categoria => categoria.Produtos)
                .HasForeignKey(produto => produto.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrupoModificador>(entity =>
        {
            entity.HasIndex(grupo => grupo.Nome).IsUnique();
            entity.HasOne(grupo => grupo.SetorImpressao).WithMany()
                .HasForeignKey(grupo => grupo.SetorImpressaoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OpcaoModificador>(entity =>
        {
            entity.Property(opcao => opcao.Preco).HasPrecision(10, 2);
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
            entity.HasOne(item => item.SetorImpressao).WithMany()
                .HasForeignKey(item => item.SetorImpressaoId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne(pagamento => pagamento.UsuarioGerenteEstorno)
                .WithMany()
                .HasForeignKey(pagamento => pagamento.UsuarioGerenteEstornoId)
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

        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.Property(movimentacao => movimentacao.Tipo).HasConversion<string>();
            entity.Property(movimentacao => movimentacao.Quantidade).HasPrecision(10, 3);
            entity.Property(movimentacao => movimentacao.SaldoApos).HasPrecision(10, 3);
            entity.Property(movimentacao => movimentacao.Observacao).HasMaxLength(250);
            entity.HasIndex(movimentacao => movimentacao.DataHora);
            entity.HasIndex(movimentacao => movimentacao.ItemPedidoId).IsUnique();
            entity.HasOne(movimentacao => movimentacao.Produto)
                .WithMany(produto => produto.MovimentacoesEstoque)
                .HasForeignKey(movimentacao => movimentacao.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movimentacao => movimentacao.ItemPedido)
                .WithMany()
                .HasForeignKey(movimentacao => movimentacao.ItemPedidoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movimentacao => movimentacao.Usuario)
                .WithMany(usuario => usuario.MovimentacoesEstoque)
                .HasForeignKey(movimentacao => movimentacao.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.Property(usuario => usuario.Nome).HasMaxLength(100).IsRequired();
            entity.Property(usuario => usuario.PinAcesso).HasMaxLength(256).IsRequired();
            entity.Property(usuario => usuario.Perfil).HasConversion<string>();
            entity.HasIndex(usuario => usuario.PinAcesso).IsUnique();
        });

    }
}
