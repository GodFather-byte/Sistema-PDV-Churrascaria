using System.Globalization;
using ChurrascariaPDV.Infrastructure;

namespace ChurrascariaPDV.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
        try
        {
            ApplicationConfiguration.Initialize();
            using var login = new OperatorDialog();
            if (login.ShowDialog() != DialogResult.OK)
                return;

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChurrascariaPDV");
            Directory.CreateDirectory(directory);
            var service = new SqlitePdvService(Path.Combine(directory, "pdv.db"));
            using var main = new Form1(service, login.OperatorName);
            System.Windows.Forms.Application.Run(main);
        }
        catch (Exception)
        {
            MessageBox.Show("Não foi possível iniciar o PDV. Verifique o acesso à pasta de dados " +
                "e a disponibilidade do banco local. O aplicativo será encerrado.",
                "Falha ao iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}