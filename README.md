# Sistema PDV Churrascaria

Aplicação Blazor Web App com interatividade no servidor, preparada para execução local em .NET 8. O projeto inclui o modelo inicial de domínio, persistência SQLite e dados de demonstração.

## Estrutura da solution

```text
ChurrascariaPDV.sln
└── src/
    └── ChurrascariaPDV/
        ├── Components/
        │   ├── Layout/
        │   │   ├── MainLayout.razor
        │   │   ├── MainLayout.razor.css
        │   │   ├── NavMenu.razor
        │   │   └── NavMenu.razor.css
        │   ├── Pages/
        │   │   ├── Counter.razor
        │   │   ├── Error.razor
        │   │   ├── Home.razor
        │   │   └── Weather.razor
        │   ├── App.razor
        │   ├── Routes.razor
        │   └── _Imports.razor
        ├── Domain/
        │   ├── Entities/
        │   │   ├── CategoriaProduto.cs
        │   │   ├── Comanda.cs
        │   │   ├── FechamentoConta.cs
        │   │   ├── GrupoModificador.cs
        │   │   ├── GrupoModificadorProduto.cs
        │   │   ├── ItemPedido.cs
        │   │   ├── Mesa.cs
        │   │   ├── MovimentacaoCaixa.cs
        │   │   ├── OpcaoModificador.cs
        │   │   ├── Pagamento.cs
        │   │   ├── Pedido.cs
        │   │   ├── Produto.cs
        │   │   ├── TurnoCaixa.cs
        │   │   └── Usuario.cs
        │   └── Enums/
        │       ├── MeioPagamento.cs
        │       ├── PerfilUsuario.cs
        │       ├── SetorImpressao.cs
        │       ├── StatusComanda.cs
        │       ├── StatusMesa.cs
        │       ├── StatusPedido.cs
        │       ├── TipoItem.cs
        │       └── TipoMovimentacaoCaixa.cs
        ├── Infrastructure/
        │   └── Data/
        │       └── AppDbContext.cs
        ├── Properties/
        │   └── launchSettings.json
        ├── wwwroot/
        │   ├── bootstrap/
        │   ├── app.css
        │   └── favicon.png
        ├── Program.cs
        ├── appsettings.Development.json
        ├── appsettings.json
        └── ChurrascariaPDV.csproj
```

## Tecnologias

- ASP.NET Core 8 Blazor Web App com Interactive Server e SignalR integrado.
- SQLite com Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`).
- QRCoder para a geração de QR Code.
- `System.Net.Sockets` e SignalR são fornecidos pelo .NET/ASP.NET Core, sem pacote adicional.

## Executar

Na raiz do repositório:

```bash
dotnet run --project src/ChurrascariaPDV
```

O Kestrel escuta em `http://0.0.0.0:5000`, permitindo acesso por outros dispositivos na rede local em `http://<IP-LAN-DO-CAIXA>:5000`. O arquivo `churrascaria.db` é criado automaticamente na primeira inicialização com `EnsureCreated`.

O seed inicial contém 50 mesas, cinco categorias, os dez produtos solicitados e grupos/opções de modificadores para carnes e bebidas. Os preços são valores iniciais de demonstração e podem ser ajustados no banco de dados.

## Pacotes NuGet

- `Microsoft.EntityFrameworkCore.Sqlite` 8.0.22
- `QRCoder` 1.6.0
