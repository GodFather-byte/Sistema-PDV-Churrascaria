namespace ChurrascariaPDV.WinForms;

internal sealed class OperatorDialog : Form
{
    private readonly TextBox _name = new() { Width = 300, MaxLength = 100 };
    public string OperatorName => _name.Text.Trim();

    public OperatorDialog()
    {
        Text = "Operador local — MOCK";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 215);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(new Label
        {
            Text = "Identificação local MOCK, sem senha ou autenticação.\n" +
                   "O nome apenas identifica as operações no histórico.",
            AutoSize = true, Margin = new Padding(0, 0, 0, 18)
        });
        content.Controls.Add(new Label { Text = "Nome do operador", AutoSize = true });
        content.Controls.Add(_name);
        var enter = new Button { Text = "Entrar", AutoSize = true };
        enter.Click += (_, _) =>
        {
            if (OperatorName.Length == 0)
            {
                MessageBox.Show(this, "Informe o nome do operador.", "Identificação",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _name.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
        };
        content.Controls.Add(enter);
        Controls.Add(content);
        AcceptButton = enter;
    }
}
