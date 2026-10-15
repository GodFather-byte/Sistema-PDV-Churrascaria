using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace ChurrascariaPDV.Services;

/// <summary>Descobre o IPv4 da rede local do PC do caixa e gera o QR Code de acesso dos garçons.</summary>
public class RedeLocalService(IConfiguration configuration)
{
    public int Porta => configuration.GetValue("Servidor:Porta", 5000);

    public IReadOnlyList<string> ObterIpsLocais()
    {
        var candidatos = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                          && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(nic => new
            {
                Propriedades = nic.GetIPProperties(),
                Virtual = EhVirtual(nic)
            })
            .SelectMany(nic => nic.Propriedades.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                .Select(a => new
                {
                    Ip = a.Address,
                    TemGateway = nic.Propriedades.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                                            && !g.Address.Equals(IPAddress.Any)),
                    nic.Virtual
                }))
            .Where(c => !c.Ip.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .OrderByDescending(c => c.TemGateway)
            .ThenBy(c => c.Virtual)
            .ThenByDescending(c => EhIpPrivado(c.Ip))
            .Select(c => c.Ip.ToString())
            .Distinct()
            .ToList();

        return candidatos.Count > 0 ? candidatos : ["127.0.0.1"];
    }

    public string ObterUrlGarcom(string? ip = null) => $"http://{ip ?? ObterIpsLocais()[0]}:{Porta}/garcom";

    public string GerarQrCodeDataUri(string conteudo)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(dados).GetGraphic(12);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    private static bool EhVirtual(NetworkInterface nic)
    {
        var descricao = (nic.Description + " " + nic.Name).ToLowerInvariant();
        return new[] { "virtual", "vmware", "hyper-v", "vbox", "docker", "wsl", "vethernet", "bluetooth" }
            .Any(descricao.Contains);
    }

    private static bool EhIpPrivado(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
