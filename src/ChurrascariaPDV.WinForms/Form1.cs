using System.Data.Common;
using ChurrascariaPDV.Application;
using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.WinForms;

public partial class Form1 : Form
{
    private readonly IPdvService _service;
    private readonly string _operator;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Label _notice = new() { AutoSize = true, Padding = new Padding(8) };
    private readonly DataGridView _products = Grid();
    private readonly TextBox _sku = TextField(130), _name = TextField(210), _category = TextField(160);
    private readonly NumericUpDown _price = Money(), _cost = Money();
    private readonly CheckBox _hasCost = new() { Text = "Informar custo", AutoSize = true };
    private readonly CheckBox _active = new() { Text = "Ativo", Checked = true, AutoSize = true };
    private readonly Label _productMode = new() { Text = "Novo produto", AutoSize = true };
    private long? _productId;
    private readonly ComboBox _stockProduct = ProductPicker(), _saleProduct = ProductPicker();
    private readonly ComboBox _stockType = Choices<StockMovementType>();
    private readonly NumericUpDown _stockQuantity = Quantity(allowNegative: true);
    private readonly TextBox _stockReason = TextField(260);
    private readonly CheckBox _filterStock = new() { Text = "Histórico só deste produto", AutoSize = true };
    private readonly DataGridView _movements = Grid();
    private readonly ComboBox _drafts = new() { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _items = Grid(), _paymentGrid = Grid();
    private readonly NumericUpDown _quantity = Quantity(), _discount = Money(), _paymentAmount = Money();
    private readonly ComboBox _paymentMethod = Choices<PaymentMethod>();
    private readonly Label _saleTitle = new() { Text = "Nenhuma venda aberta na tela.", AutoSize = true };
    private readonly Label _totals = new() { AutoSize = true };
    private readonly Label _paymentTotals = new() { AutoSize = true };
    private readonly List<Payment> _payments = [];
    private Sale? _sale;
    private readonly Label _cashState = new() { AutoSize = true };
    private readonly NumericUpDown _opening = Money(), _cashAmount = Money(), _counted = Money();
    private readonly NumericUpDown _summaryId = new() { Minimum = 0, Maximum = long.MaxValue, Width = 140 };
    private readonly ComboBox _cashType = Choices<CashMovementType>();
    private readonly TextBox _cashReason = TextField(260);
    private readonly Label _cashSummary = new() { AutoSize = true, Padding = new Padding(10) };
    private readonly DateTimePicker _from = DatePicker(DateTime.Today), _to = DatePicker(DateTime.Today);
    private readonly DataGridView _salesReport = Grid(), _bestSellers = Grid(), _stockReport = Grid();

    public Form1(IPdvService service, string operatorName)
    {
        _service = service;
        _operator = operatorName;
        InitializeComponent();
        Text = $"Churrascaria PDV — Operador local MOCK: {_operator}";
        Size = new Size(1250, 850);
        MinimumSize = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = $"Operador: {_operator} (MOCK, sem autenticação)  |  F2 Nova venda  |  F4 Adicionar item  |  F8 Finalizar",
            AutoSize = true, Padding = new Padding(8)
        }, 0, 0);
        root.Controls.Add(_tabs, 0, 1);
        root.Controls.Add(_notice, 0, 2);
        Controls.Add(root);
        BuildProducts();
        BuildStock();
        BuildPos();
        BuildCash();
        BuildReports();
        Shown += (_, _) => Run(RefreshAll);
        KeyDown += OnShortcut;
        FormClosing += (_, e) =>
        {
            if (HasLocalChanges && !Confirm(
                "Sair e descartar os pagamentos propostos e o desconto ainda não aplicado? " +
                "Os itens e o desconto já aplicado do rascunho continuam salvos."))
                e.Cancel = true;
        };
    }

    private void BuildProducts()
    {
        Configure(_products, ("Id", "ID", null), ("Sku", "SKU", null), ("Name", "Nome", null),
            ("Category", "Categoria", null), ("Price", "Preço", "C2"), ("Cost", "Custo", "C2"),
            ("Active", "Ativo", null), ("Stock", "Estoque (un.)", null));
        var editor = Stack(
            Bar(_productMode, Button("Novo / limpar", ClearProduct)),
            Bar(Field("SKU", _sku), Field("Nome", _name), Field("Categoria", _category)),
            Bar(Field("Preço", _price), _hasCost, Field("Custo opcional", _cost), _active,
                Button("Salvar", SaveProduct), Button("Excluir selecionado", DeleteProduct),
                Button("Atualizar", RefreshProducts)));
        _cost.Enabled = false;
        _hasCost.CheckedChanged += (_, _) => _cost.Enabled = _hasCost.Checked;
        _products.SelectionChanged += (_, _) => Run(() =>
        {
            if (Selected<Product>(_products) is not { } product) return;
            _productId = product.Id;
            _productMode.Text = $"Editando produto #{product.Id}";
            _sku.Text = product.Sku;
            _name.Text = product.Name;
            _category.Text = product.Category;
            _price.Value = product.Price;
            _hasCost.Checked = product.Cost.HasValue;
            _cost.Value = product.Cost ?? 0;
            _active.Checked = product.Active;
        });
        AddPage("Produtos", editor, _products);
    }

    private void SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(_sku.Text) || string.IsNullOrWhiteSpace(_name.Text))
            throw new DomainException("Informe SKU e nome do produto.");
        _service.SaveProduct(_productId,
            new ProductInput(_sku.Text.Trim(), _name.Text.Trim(), _category.Text.Trim(),
                _price.Value, _hasCost.Checked ? _cost.Value : null, _active.Checked), _operator);
        RefreshProducts();
        ClearProduct();
        Notice("Produto salvo.");
    }

    private void DeleteProduct()
    {
        var product = RequireSelection<Product>(_products, "Selecione o produto para excluir.");
        if (!Confirm($"Excluir o produto {product.Sku} — {product.Name}? Esta ação não é desfeita pela tela.")) return;
        _service.DeleteProduct(product.Id, _operator);
        RefreshProducts();
        ClearProduct();
        Notice("Produto excluído.");
    }

    private void ClearProduct()
    {
        _products.ClearSelection();
        _productId = null;
        _productMode.Text = "Novo produto";
        _sku.Clear();
        _name.Clear();
        _category.Clear();
        _price.Value = _cost.Value = 0;
        _hasCost.Checked = false;
        _active.Checked = true;
    }

    private void RefreshProducts()
    {
        var products = _service.GetProducts();
        Bind(_products, products);
        FillProducts(_stockProduct, products);
        FillProducts(_saleProduct, products.Where(product => product.Active));
    }

    private void BuildStock()
    {
        Configure(_movements, ("Id", "ID", null), ("ProductName", "Produto", null),
            ("Type", "Movimento", null), ("Quantity", "Quantidade / delta (un.)", null),
            ("Reason", "Motivo", null), ("Operator", "Operador", null), ("CreatedAt", "Data local", null));
        var editor = Stack(
            Bar(Field("Produto (SKU — nome)", _stockProduct), Field("Tipo", _stockType),
                Field("Quantidade / delta", _stockQuantity)),
            Bar(Field("Motivo obrigatório", _stockReason), Button("Registrar movimento", MoveStock),
                _filterStock, Button("Atualizar histórico", RefreshStock)),
            Bar(new Label
            {
                AutoSize = true,
                Text = "Entrada/Saída: quantidade positiva em unidades. Ajuste: DELTA com sinal (+ acrescenta, − reduz), não saldo final."
            }));
        AddPage("Estoque / histórico", editor, _movements);
    }

    private void MoveStock()
    {
        var productId = PickProduct(_stockProduct);
        var type = Pick<StockMovementType>(_stockType);
        var quantity = decimal.ToInt32(_stockQuantity.Value);
        if (quantity == 0 || (type != StockMovementType.Adjustment && quantity < 0))
            throw new DomainException("Entrada/saída exige quantidade positiva; ajuste exige delta diferente de zero.");
        var reason = RequiredReason(_stockReason.Text);
        if (!Confirm($"Registrar {Label(type)} de {quantity} unidade(s) em {_stockProduct.Text}?")) return;
        _service.MoveStock(productId, type, quantity, reason, _operator);
        _stockReason.Clear();
        RefreshProducts();
        RefreshStock();
        Notice("Movimento de estoque registrado.");
    }

    private void RefreshStock() =>
        Bind(_movements, _service.GetStockMovements(_filterStock.Checked ? PickProduct(_stockProduct) : null));

    private void BuildPos()
    {
        Configure(_items, ("Sku", "SKU", null), ("Name", "Produto", null),
            ("Quantity", "Quantidade (un.)", null), ("UnitPrice", "Preço unitário", "C2"), ("Subtotal", "Subtotal", "C2"));
        Configure(_paymentGrid, ("Method", "Forma", null), ("Amount", "Valor recebido", "C2"));
        _items.SelectionChanged += (_, _) =>
        {
            if (Selected<SaleItem>(_items) is { } item) _quantity.Value = item.Quantity;
        };
        _paymentGrid.SelectionChanged += (_, _) =>
        {
            if (Selected<Payment>(_paymentGrid) is not { } payment) return;
            _paymentAmount.Value = payment.Amount;
            SelectChoice(_paymentMethod, payment.Method);
        };
        var top = Stack(
            Bar(Button("Nova venda (F2)", NewSale), Field("Rascunhos persistidos", _drafts),
                Button("Retomar selecionado", ResumeSale), Button("Atualizar rascunhos", RefreshDrafts),
                Button("Recarregar venda atual", ReloadSale)),
            Bar(_saleTitle),
            Bar(Field("Produto: SKU exato ou lista (SKU — nome)", _saleProduct), Field("Quantidade inteira", _quantity),
                Button("Adicionar (F4)", AddItem), Button("Alterar qtd. selecionada", UpdateItem),
                Button("Remover selecionado", RemoveItem)),
            Bar(Field("Desconto em R$", _discount), Button("Aplicar desconto", ApplyDiscount), _totals));
        var bottom = Stack(
            Bar(new Label
            {
                AutoSize = true,
                Text = "Pagamentos abaixo são propostas locais; só são registrados ao finalizar. Troco somente em dinheiro."
            }),
            Bar(Field("Forma", _paymentMethod), Field("Valor", _paymentAmount),
                Button("Adicionar pagamento", AddPayment), Button("Alterar selecionado", UpdatePayment),
                Button("Remover selecionado", RemovePayment)),
            Bar(_paymentTotals, Button("Finalizar (F8)", Checkout), Button("Cancelar venda aberta", CancelCurrentSale)));
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        body.Controls.Add(_items, 0, 0);
        body.Controls.Add(bottom, 0, 1);
        body.Controls.Add(_paymentGrid, 0, 2);
        AddPage("PDV / vendas", top, body);
        RenderSale();
    }

    private bool HasLocalChanges => _payments.Count > 0 || (_sale is not null && _discount.Value != _sale.Discount);

    private bool CanSwitchSale() => !HasLocalChanges || Confirm(
        "Trocar de venda e descartar os pagamentos propostos e o desconto ainda não aplicado? " +
        "Os itens e o desconto já aplicado do rascunho atual continuam salvos.");

    private void NewSale()
    {
        if (!CanSwitchSale()) return;
        var sale = _service.CreateSale(_operator);
        _payments.Clear();
        _sale = sale;
        _tabs.SelectedIndex = 2;
        RenderSale();
        RefreshDrafts();
        Notice("Rascunho criado e salvo. Itens e desconto são salvos a cada operação.");
    }

    private void ResumeSale()
    {
        if (_drafts.SelectedItem is not DraftChoice draft)
            throw new DomainException("Selecione um rascunho para retomar.");
        if (_sale?.Id == draft.Id) return;
        if (!CanSwitchSale()) return;
        var sale = _service.GetSale(draft.Id);
        if (sale.Status != SaleStatus.Open) throw new DomainException("Esta venda não está mais aberta. Atualize os rascunhos.");
        _sale = sale;
        _payments.Clear();
        RenderSale();
        Notice($"Rascunho #{sale.Id} retomado.");
    }

    private Sale CurrentSale() => _sale is { Status: SaleStatus.Open } sale
        ? sale : throw new DomainException("Crie ou retome uma venda aberta primeiro.");

    private void ReloadSale()
    {
        var current = CurrentSale();
        if (HasLocalChanges && !Confirm(
            "Recarregar o rascunho salvo e descartar os pagamentos propostos e o desconto ainda não aplicado?")) return;
        var sale = _service.GetSale(current.Id);
        _sale = sale.Status == SaleStatus.Open ? sale : null;
        _payments.Clear();
        RenderSale();
        RefreshDrafts();
        Notice(sale.Status == SaleStatus.Open
            ? $"Venda #{sale.Id} recarregada."
            : $"Venda #{sale.Id} está {Label(sale.Status).ToLowerInvariant()}. Consulte o relatório.");
    }

    private void AddItem()
    {
        var sale = CurrentSale();
        var id = PickProduct(_saleProduct);
        var existing = sale.Items.FirstOrDefault(item => item.ProductId == id)?.Quantity ?? 0;
        var quantity = checked(existing + decimal.ToInt32(_quantity.Value));
        _sale = _service.SetItemQuantity(sale.Id, id, quantity, _operator);
        RenderSale(resetDiscount: false);
    }

    private void UpdateItem()
    {
        var sale = CurrentSale();
        var item = RequireSelection<SaleItem>(_items, "Selecione o item cuja quantidade deseja alterar.");
        _sale = _service.SetItemQuantity(sale.Id, item.ProductId, decimal.ToInt32(_quantity.Value), _operator);
        RenderSale(resetDiscount: false);
    }

    private void RemoveItem()
    {
        var sale = CurrentSale();
        var item = RequireSelection<SaleItem>(_items, "Selecione um item para remover.");
        if (!Confirm($"Remover {item.Sku} — {item.Name} da venda #{sale.Id}?")) return;
        _sale = _service.SetItemQuantity(sale.Id, item.ProductId, 0, _operator);
        RenderSale(resetDiscount: false);
    }

    private void ApplyDiscount()
    {
        _sale = _service.SetDiscount(CurrentSale().Id, _discount.Value, _operator);
        RenderSale();
    }

    private Payment PaymentInput()
    {
        CurrentSale();
        if (_paymentAmount.Value <= 0) throw new DomainException("O pagamento deve ser maior que zero.");
        return new Payment(Pick<PaymentMethod>(_paymentMethod), _paymentAmount.Value);
    }

    private void AddPayment()
    {
        _payments.Add(PaymentInput());
        RenderPayments();
    }

    private void UpdatePayment()
    {
        var index = _paymentGrid.CurrentRow?.Index ?? -1;
        if (index < 0 || !_paymentGrid.CurrentRow!.Selected)
            throw new DomainException("Selecione o pagamento para alterar.");
        _payments[index] = PaymentInput();
        RenderPayments();
    }

    private void RemovePayment()
    {
        RequireSelection<Payment>(_paymentGrid, "Selecione o pagamento para remover.");
        var index = _paymentGrid.CurrentRow!.Index;
        if (!Confirm("Remover este pagamento ainda não registrado?")) return;
        _payments.RemoveAt(index);
        RenderPayments();
    }

    private void Checkout()
    {
        var sale = CurrentSale();
        if (_discount.Value != sale.Discount)
            throw new DomainException("O desconto na tela ainda não foi aplicado. Clique em Aplicar desconto antes de finalizar.");
        if (_payments.Count == 0) throw new DomainException("Informe pelo menos um pagamento.");
        var received = _payments.Sum(payment => payment.Amount);
        var change = Math.Max(0, received - sale.Total);
        if (received < sale.Total) throw new DomainException("Os pagamentos não cobrem o total da venda.");
        if (change > _payments.Where(payment => payment.Method == PaymentMethod.Cash).Sum(payment => payment.Amount))
            throw new DomainException("O troco não pode exceder o valor recebido em dinheiro.");
        if (!Confirm($"Finalizar venda #{sale.Id}?\nTotal: {sale.Total:C2}\nRecebido: {received:C2}\nTroco: {change:C2}")) return;
        var completed = _service.CompleteSale(sale.Id, _payments.ToArray(), _operator);
        _sale = null;
        _payments.Clear();
        RenderSale();
        Notice($"Venda #{completed.Id} concluída. Troco: {completed.Change:C2}.");
        RefreshDrafts();
        RefreshProducts();
        RefreshCash();
    }

    private void CancelCurrentSale()
    {
        var sale = CurrentSale();
        var reason = AskReason($"Cancelar venda aberta #{sale.Id}");
        if (reason is null || !Confirm($"Cancelar a venda #{sale.Id}? Itens e pagamentos propostos serão descartados.")) return;
        _service.CancelSale(sale.Id, reason, _operator);
        _sale = null;
        _payments.Clear();
        RenderSale();
        RefreshDrafts();
        Notice($"Venda #{sale.Id} cancelada.");
    }

    private void RenderSale(bool resetDiscount = true)
    {
        _saleTitle.Text = _sale is null ? "Nenhuma venda aberta na tela." :
            $"Venda #{_sale.Id} — caixa #{_sale.CashSessionId} — operador {_sale.Operator} — {LocalTime(_sale.CreatedAt):g}";
        Bind(_items, _sale?.Items ?? []);
        if (resetDiscount) _discount.Value = _sale?.Discount ?? 0;
        _totals.Text = $"Subtotal: {_sale?.Subtotal ?? 0:C2}  |  Total: {_sale?.Total ?? 0:C2}";
        RenderPayments();
    }

    private void RenderPayments()
    {
        Bind(_paymentGrid, _payments.ToArray());
        var received = _payments.Sum(payment => payment.Amount);
        var total = _sale?.Total ?? 0;
        _paymentTotals.Text = $"Recebido: {received:C2}  |  Falta: {Math.Max(0, total - received):C2}  |  Troco: {Math.Max(0, received - total):C2}";
    }

    private void RefreshDrafts()
    {
        var selected = (_drafts.SelectedItem as DraftChoice)?.Id;
        _drafts.Items.Clear();
        foreach (var sale in _service.GetOpenSales())
            _drafts.Items.Add(new DraftChoice(sale.Id,
                $"#{sale.Id} — {LocalTime(sale.CreatedAt):g} — {sale.Operator} — {sale.Total:C2}"));
        _drafts.SelectedItem = _drafts.Items.Cast<DraftChoice>().FirstOrDefault(draft => draft.Id == selected);
    }

    private void BuildCash()
    {
        var top = Stack(
            Bar(_cashState, Button("Atualizar caixa", RefreshCash)),
            Bar(Field("Fundo inicial", _opening), Button("Abrir caixa", OpenCash)),
            Bar(Field("Movimento", _cashType), Field("Valor", _cashAmount),
                Field("Motivo obrigatório", _cashReason), Button("Registrar", MoveCash)),
            Bar(Field("Dinheiro contado (inclui fundo)", _counted), Button("Fechar caixa", CloseCash)),
            Bar(Field("Consultar resumo do caixa ID", _summaryId), Button("Consultar resumo", ShowCashSummary)),
            Bar(new Label
            {
                AutoSize = true,
                Text = "Fechamento exige resolver todas as vendas abertas. Diferença = contado − esperado em dinheiro.\n" +
                    "Após fechar, o resumo permanece disponível pelo ID (inclusive após reiniciar)."
            }));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(_cashSummary);
        AddPage("Caixa", top, scroll);
    }

    private void OpenCash()
    {
        if (!Confirm($"Abrir caixa para {_operator} com fundo inicial de {_opening.Value:C2}?")) return;
        var session = _service.OpenCash(_opening.Value, _operator);
        _summaryId.Value = session.Id;
        RefreshCash();
        Notice($"Caixa #{session.Id} aberto.");
    }

    private void MoveCash()
    {
        var type = Pick<CashMovementType>(_cashType);
        var reason = RequiredReason(_cashReason.Text);
        if (_cashAmount.Value <= 0) throw new DomainException("Informe valor maior que zero.");
        if (!Confirm($"Registrar {Label(type)} de {_cashAmount.Value:C2} no caixa aberto?")) return;
        _service.MoveCash(type, _cashAmount.Value, reason, _operator);
        _cashReason.Clear();
        if (_service.GetOpenCashSession() is { } session) _summaryId.Value = session.Id;
        RefreshCash();
        Notice("Movimento de caixa registrado.");
    }

    private void CloseCash()
    {
        var session = _service.GetOpenCashSession() ?? throw new DomainException("Não há caixa aberto.");
        if (_service.GetOpenSales().Count > 0)
            throw new DomainException("Há vendas pendentes. Retome e finalize ou cancele os rascunhos antes de fechar o caixa.");
        var summary = _service.GetCashSummary(session.Id);
        if (!Confirm($"Fechar caixa #{session.Id}?\nEsperado: {summary.ExpectedCash:C2}\n" +
            $"Contado: {_counted.Value:C2}\nDiferença: {_counted.Value - summary.ExpectedCash:C2}")) return;
        var closed = _service.CloseCash(_counted.Value, _operator);
        _summaryId.Value = closed.SessionId;
        RenderCashSummary(closed);
        RefreshCash();
        Notice($"Caixa #{closed.SessionId} fechado. Guarde o ID para consultar o resumo.");
    }

    private void RefreshCash()
    {
        var session = _service.GetOpenCashSession();
        _cashState.Text = session is null ? "Nenhum caixa aberto." :
            $"Caixa aberto #{session.Id} — {session.Operator} — {LocalTime(session.OpenedAt):g}";
        if (_summaryId.Value == 0 && session is not null) _summaryId.Value = session.Id;
        if (_summaryId.Value > 0) ShowCashSummary();
    }

    private void ShowCashSummary()
    {
        if (_summaryId.Value <= 0) throw new DomainException("Informe um ID de caixa maior que zero.");
        RenderCashSummary(_service.GetCashSummary(decimal.ToInt64(_summaryId.Value)));
    }

    private void RenderCashSummary(CashSummary summary)
    {
        var lines = new List<string>
        {
            $"Resumo do caixa #{summary.SessionId} — {(summary.CountedCash.HasValue ? "fechado" : "aberto")}",
            $"Fundo inicial: {summary.OpeningAmount:C2}", "", "Vendas líquidas por forma de pagamento:"
        };
        foreach (var method in Enum.GetValues<PaymentMethod>())
            lines.Add($"{Label(method)}: {summary.SalesByMethod.GetValueOrDefault(method):C2}");
        lines.AddRange(["", $"Suprimentos: {summary.Supplies:C2}", $"Sangrias: {summary.Withdrawals:C2}",
            $"Dinheiro esperado: {summary.ExpectedCash:C2}",
            $"Dinheiro contado: {(summary.CountedCash.HasValue ? summary.CountedCash.Value.ToString("C2") : "não contado")}",
            $"Diferença: {(summary.Difference.HasValue ? summary.Difference.Value.ToString("C2") : "não apurada")}"]);
        _cashSummary.Text = string.Join(Environment.NewLine, lines);
    }

    private void BuildReports()
    {
        Configure(_salesReport, ("Id", "Venda ID", null), ("CreatedAt", "Data local", null),
            ("Operator", "Operador", null), ("Status", "Situação", null), ("Subtotal", "Subtotal", "C2"),
            ("Discount", "Desconto", "C2"), ("Total", "Total", "C2"));
        Configure(_bestSellers, ("ProductId", "Produto ID", null), ("Name", "Produto", null),
            ("Quantity", "Unidades", null), ("GrossRevenue", "Receita bruta (antes de descontos)", "C2"));
        Configure(_stockReport, ("Sku", "SKU", null), ("Name", "Produto", null),
            ("Category", "Categoria", null), ("Active", "Ativo", null), ("Stock", "Saldo (un.)", null));
        var reportTabs = new TabControl { Dock = DockStyle.Fill };
        foreach (var (caption, grid) in new[] { ("Vendas", _salesReport), ("Mais vendidos", _bestSellers), ("Estoque atual", _stockReport) })
        {
            var tab = new TabPage(caption);
            tab.Controls.Add(grid);
            reportTabs.TabPages.Add(tab);
        }
        AddPage("Relatórios", Stack(
            Bar(Field("De (data local)", _from), Field("Até (inclusive)", _to),
                Button("Atualizar relatórios", RefreshReports), Button("Cancelar venda concluída selecionada", CancelCompleted)),
            Bar(new Label
            {
                Text = "Vendas e mais vendidos usam o período. Estoque é o saldo atual, independentemente das datas.",
                AutoSize = true
            })), reportTabs);
    }

    private void RefreshReports()
    {
        if (_to.Value.Date < _from.Value.Date) throw new DomainException("A data final deve ser igual ou posterior à inicial.");
        // Local midnight boundaries are converted separately to respect daylight-saving offsets.
        var start = DateTime.SpecifyKind(_from.Value.Date, DateTimeKind.Local).ToUniversalTime();
        var end = DateTime.SpecifyKind(_to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        Bind(_salesReport, _service.GetSales(start, end));
        Bind(_bestSellers, _service.GetBestSellers(start, end));
        Bind(_stockReport, _service.GetProducts());
        Notice("Relatórios atualizados.");
    }

    private void CancelCompleted()
    {
        var sale = RequireSelection<SaleReport>(_salesReport, "Selecione uma venda na aba Vendas do relatório.");
        if (sale.Status != SaleStatus.Completed) throw new DomainException("Selecione uma venda concluída. Rascunhos são cancelados no PDV.");
        var reason = AskReason($"Cancelar venda concluída #{sale.Id}");
        if (reason is null || !Confirm($"Cancelar venda CONCLUÍDA #{sale.Id}, de {sale.Total:C2}?\n" +
            "O cancelamento estorna estoque e valores conforme as regras do caixa. Confirma?")) return;
        _service.CancelSale(sale.Id, reason, _operator);
        RefreshReports();
        RefreshProducts();
        RefreshCash();
        Notice($"Venda #{sale.Id} cancelada.");
    }

    private void RefreshAll()
    {
        RefreshProducts();
        ClearProduct();
        RefreshStock();
        RefreshDrafts();
        RefreshCash();
        RefreshReports();
        Notice("Pronto. Abra um caixa para iniciar vendas.");
    }

    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None) return;
        Action? action = e.KeyCode switch
        {
            Keys.F2 => NewSale,
            Keys.F4 when _tabs.SelectedIndex == 2 => AddItem,
            Keys.F8 when _tabs.SelectedIndex == 2 => Checkout,
            _ => null
        };
        if (action is null) return;
        e.Handled = e.SuppressKeyPress = true;
        Run(action);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (DomainException exception) { Error(exception.Message, MessageBoxIcon.Warning); }
        catch (DbException) { Error("Falha no banco de dados local. Verifique o acesso e tente atualizar a tela."); }
        catch (IOException) { Error("Falha ao acessar os dados locais. Verifique o armazenamento e tente novamente."); }
        catch (UnauthorizedAccessException) { Error("Sem permissão para acessar os dados locais."); }
        catch (Exception) { Error("Falha na operação ou na atualização da tela. Confira os dados salvos antes de tentar novamente."); }
    }

    private void Error(string message, MessageBoxIcon icon = MessageBoxIcon.Error)
    {
        Notice("Confira o estado salvo e atualize a tela antes de repetir a operação.");
        MessageBox.Show(this, message, "Churrascaria PDV", MessageBoxButtons.OK, icon);
    }

    private void Notice(string message) => _notice.Text = message;
    private bool Confirm(string message) => MessageBox.Show(this, message, "Confirmar operação",
        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private string? AskReason(string caption)
    {
        using var dialog = new Form
        {
            Text = caption, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(460, 160), FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false
        };
        var reason = TextField(400);
        var ok = new Button { Text = "Continuar", AutoSize = true };
        var cancel = new Button { Text = "Voltar", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(reason.Text))
            {
                MessageBox.Show(dialog, "Informe o motivo obrigatório.", caption);
                return;
            }
            dialog.DialogResult = DialogResult.OK;
        };
        dialog.Controls.Add(Stack(Bar(Field("Motivo obrigatório", reason)), Bar(ok, cancel)));
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? reason.Text.Trim() : null;
    }

    private static string RequiredReason(string reason) => !string.IsNullOrWhiteSpace(reason)
        ? reason.Trim() : throw new DomainException("Informe o motivo obrigatório.");
    private static T? Selected<T>(DataGridView grid) where T : class =>
        grid.Tag is not true && grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].DataBoundItem as T : null;
    private static T RequireSelection<T>(DataGridView grid, string message) where T : class =>
        Selected<T>(grid) ?? throw new DomainException(message);
    private static long PickProduct(ComboBox picker)
    {
        if (picker.SelectedItem is ProductChoice choice && picker.Text == choice.ToString()) return choice.Id;
        var text = picker.Text.Trim();
        var matches = picker.Items.Cast<ProductChoice>().Where(item =>
            string.Equals(item.Sku, text, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 1) return matches[0].Id;
        throw new DomainException("Selecione um produto na lista ou digite um SKU exato existente.");
    }

    private static void FillProducts(ComboBox picker, IEnumerable<Product> products)
    {
        var selected = (picker.SelectedItem as ProductChoice)?.Id;
        var text = picker.Text;
        picker.Items.Clear();
        foreach (var product in products.OrderBy(product => product.Sku))
            picker.Items.Add(new ProductChoice(product.Id, product.Sku, product.Name));
        picker.SelectedItem = picker.Items.Cast<ProductChoice>().FirstOrDefault(product => product.Id == selected);
        if (picker.SelectedIndex < 0) picker.Text = text;
    }

    private static ComboBox ProductPicker() => new()
    {
        Width = 330, DropDownStyle = ComboBoxStyle.DropDown,
        AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems
    };
    private static ComboBox Choices<T>() where T : struct, Enum
    {
        var combo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<T>()) combo.Items.Add(new EnumChoice<T>(value, Label(value)));
        combo.SelectedIndex = 0;
        return combo;
    }
    private static T Pick<T>(ComboBox combo) where T : struct, Enum =>
        combo.SelectedItem is EnumChoice<T> choice ? choice.Value : throw new DomainException("Selecione uma opção.");
    private static void SelectChoice<T>(ComboBox combo, T value) where T : struct, Enum =>
        combo.SelectedItem = combo.Items.Cast<EnumChoice<T>>().FirstOrDefault(choice => choice.Value.Equals(value));
    private static string Label(object value) => value switch
    {
        PaymentMethod.Cash => "Dinheiro", PaymentMethod.CreditCard => "Crédito",
        PaymentMethod.DebitCard => "Débito", PaymentMethod.Pix => "Pix",
        StockMovementType.Entry => "Entrada", StockMovementType.Exit => "Saída",
        StockMovementType.Adjustment => "Ajuste (delta)",
        CashMovementType.Supply => "Suprimento", CashMovementType.Withdrawal => "Sangria",
        SaleStatus.Open => "Aberta", SaleStatus.Completed => "Concluída", SaleStatus.Cancelled => "Cancelada",
        _ => value.ToString() ?? ""
    };
    private static DateTime LocalTime(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
    private static DateTimePicker DatePicker(DateTime date) => new()
    {
        Value = date, Format = DateTimePickerFormat.Short, Width = 125,
        MinDate = new DateTime(2000, 1, 1), MaxDate = new DateTime(2100, 12, 31)
    };
    private static NumericUpDown Money() => new()
    {
        DecimalPlaces = 2, Minimum = 0, Maximum = 999_999_999.99m, Width = 125, ThousandsSeparator = true
    };
    private static NumericUpDown Quantity(bool allowNegative = false) => new()
    {
        DecimalPlaces = 0, Minimum = allowNegative ? -int.MaxValue : 1,
        Maximum = int.MaxValue, Value = 1, Width = 115, ThousandsSeparator = true
    };
    private static TextBox TextField(int width) => new() { Width = width, MaxLength = 200 };
    private Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(5, 18, 5, 4) };
        button.Click += (_, _) => Run(action);
        return button;
    }
    private static Control Field(string caption, Control input)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Margin = new Padding(4), Padding = Padding.Empty
        };
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = Padding.Empty });
        panel.Controls.Add(input);
        return panel;
    }
    private static FlowLayoutPanel Bar(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        panel.Controls.AddRange(controls);
        return panel;
    }
    private static TableLayoutPanel Stack(params Control[] rows)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, RowCount = rows.Length,
            Padding = new Padding(4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows.Length; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(rows[i], 0, i);
        }
        return panel;
    }
    private void AddPage(string title, Control header, Control body)
    {
        var page = new TabPage(title) { Padding = new Padding(6), AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(body, 0, 1);
        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
    }
    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window, AllowUserToOrderColumns = true
    };
    private static void Configure(DataGridView grid, params (string Property, string Caption, string? Format)[] columns)
    {
        foreach (var column in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = column.Property, HeaderText = column.Caption, SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { Format = column.Format ?? "", NullValue = "—" },
                MinimumWidth = 65
            });
        grid.CellFormatting += (_, e) =>
        {
            if (e.Value is Enum value) { e.Value = Label(value); e.FormattingApplied = true; }
            else if (e.Value is DateTime date) { e.Value = LocalTime(date).ToString("g"); e.FormattingApplied = true; }
            else if (e.Value is bool active) { e.Value = active ? "Sim" : "Não"; e.FormattingApplied = true; }
        };
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; };
    }
    private static void Bind<T>(DataGridView grid, IEnumerable<T> rows)
    {
        grid.Tag = true;
        try
        {
            grid.DataSource = rows.ToList();
            grid.ClearSelection();
        }
        finally { grid.Tag = null; }
    }
    private sealed record ProductChoice(long Id, string Sku, string Name)
    {
        public override string ToString() => $"{Sku} — {Name}";
    }
    private sealed record DraftChoice(long Id, string Caption)
    {
        public override string ToString() => Caption;
    }
    private sealed record EnumChoice<T>(T Value, string Caption)
    {
        public override string ToString() => Caption;
    }
}
