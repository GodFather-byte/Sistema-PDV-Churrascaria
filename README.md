# PDV Churrascaria

O repositório reúne dois projetos independentes: o MVP desktop descrito abaixo
e a base web Blazor trazida de `main`, documentada ao final. Eles não compartilham
o banco nem sincronizam dados. A solução inclui ambos os projetos.

MVP desktop em C# / .NET 10, WinForms e SQLite, para um caixa local de churrascaria.
Não é um emissor fiscal nem uma integração com adquirentes: cartão e PIX são
registros manuais de pagamentos já confirmados pelo operador.

## Pré-requisitos e execução

- Windows com o SDK .NET 10 para desenvolver/executar pelo código-fonte.
- Para executar também a base web, instale o runtime ASP.NET Core 8 (ou SDK .NET 8).
- Não é necessário instalar um servidor de banco de dados.

Na raiz do repositório:

```powershell
dotnet restore ChurrascariaPDV.sln
dotnet build ChurrascariaPDV.sln --configuration Release
dotnet run --project src/ChurrascariaPDV.WinForms
```

O login é **mock**: informe o nome do operador. Não há senha, autenticação real
ou controle de permissões. Não use esse login como barreira de segurança.
O primeiro acesso cria o banco e aplica o schema automaticamente. Os cadastros
começam vazios.

O banco fica em `%LOCALAPPDATA%\ChurrascariaPDV\pdv.db`. Proteja essa pasta com
as permissões do Windows. Para backup, feche a aplicação e copie o banco
e eventuais arquivos auxiliares SQLite juntos; não copie apenas o `.db` enquanto
estiver em uso. Não exclua o banco para atualizar o programa.

Para distribuir uma pasta executável para Windows x64:

```powershell
dotnet publish src/ChurrascariaPDV.WinForms --configuration Release --runtime win-x64 --self-contained true --output ./publish
```

## Funcionalidades

- **Produtos:** SKU único, nome, categoria, preço, custo opcional e situação
  ativo/inativo; cadastro, consulta, edição e exclusão lógica (inativação),
  preservando o histórico e permitindo reativação.
- **Estoque:** saldo por produto, entradas, saídas e ajustes com motivo,
  operador e data/hora; histórico das movimentações. Entrada/saída recebem
  quantidades positivas; ajuste recebe uma variação assinada (por exemplo,
  `-2` reduz duas unidades), não um saldo absoluto.
- **PDV:** vendas em aberto persistentes, inclusão/remoção e alteração de
  quantidade de itens, desconto em reais, subtotal e total.
- **Pagamentos:** dinheiro, crédito, débito e PIX, inclusive pagamentos mistos.
  O excedente somente pode ser devolvido como troco da parcela em dinheiro.
- **Caixa:** uma sessão aberta por banco, fundo inicial, sangria, suprimento,
  totais por meio de pagamento, saldo esperado, valor contado e diferença.
- **Relatórios:** vendas por período, produtos mais vendidos e posição de
  estoque. Cancelamentos ficam identificados; não contam como receita efetiva.

Atalhos: **F2** abre uma nova venda; na aba PDV, **F4** adiciona o produto
selecionado e **F8** solicita a conclusão. As operações destrutivas pedem confirmação.
Pagamentos propostos na tela só são persistidos ao concluir a venda; os itens
e o desconto aplicado do rascunho são salvos a cada operação.

### Regras importantes

- Quantidades são **unidades inteiras** neste MVP. Venda por peso exige uma
  evolução explícita do modelo, não arredondamento de quilos para unidades.
- Produto inativo não pode ser vendido. Uma venda vazia, desconto inválido ou
  pagamento insuficiente não pode ser concluído.
- O estoque só é baixado na conclusão, em transação com a venda e seus
  pagamentos. Editar ou cancelar rascunhos não movimenta estoque.
- Cancelar uma venda concluída repõe o estoque uma única vez e a retira do
  resumo financeiro. Isso só é permitido enquanto o caixa original estiver
  aberto; após o fechamento, é necessário um fluxo de devolução futuro.
- Não é permitido fechar caixa com vendas em aberto. Conclua-as ou cancele-as.
- Estoque negativo é bloqueado por padrão. A composição do serviço oferece
  `allowNegativeStock`, explicitamente `false` na interface; não há liberação
  implícita no caixa.
- Valores monetários usam `decimal` nas regras e inteiros em **centavos** no
  SQLite, sem aritmética financeira em ponto flutuante.
- Operações críticas são transacionais, com validação e SQL parametrizado.
  Operações principais registram operador e horário UTC; a interface apresenta
  os horários locais.

## Estrutura e decisões técnicas

```text
ChurrascariaPDV.sln
src/
  ChurrascariaPDV.Domain/          Modelos e regras de domínio
  ChurrascariaPDV.Application/     Casos de uso e contratos de persistência
  ChurrascariaPDV.Infrastructure/  SQLite, transações e schema inicial
  ChurrascariaPDV.WinForms/        Login, navegação e telas operacionais
tests/
  ChurrascariaPDV.Tests/           Testes de domínio e integração SQLite
```

A UI chama os casos de uso, sem executar SQL. SQLite foi escolhido para execução
local sem serviços externos. O schema inicial faz parte da Infrastructure e é
aplicado automaticamente; mudanças futuras devem ser adicionadas como migrações,
preservando os dados existentes. Os preços dos itens são fotografados na venda:
alterar o cadastro não reescreve vendas anteriores.

Não são armazenados números de cartões, dados bancários ou credenciais de
pagamento. O nome informado no login identifica as operações, mas não comprova
a identidade do operador.

## Testes automatizados

Os testes do núcleo podem rodar também em Linux, sem WinForms:

```sh
dotnet test tests/ChurrascariaPDV.Tests --configuration Release
```

Compilação cruzada da solução em Linux (não permite executar a interface):

```sh
dotnet build ChurrascariaPDV.sln --configuration Release -p:EnableWindowsTargeting=true
```

Os testes usam bancos temporários isolados, sem alterar o banco operacional.
Cobrem totais, descontos, pagamentos mistos, troco, estoque e cancelamentos,
incluindo falhas que não devem deixar alterações parciais.

## Roteiro de teste manual no Windows

1. Entre como `Operador teste`. Cadastre `ESP001 / Espeto / Espetos / R$ 15,00`
   e `REF001 / Refrigerante / Bebidas / R$ 5,00`, ambos ativos.
2. Dê entrada de 10 unidades em cada produto, informando um motivo; consulte
   saldos e histórico.
3. Abra o caixa com R$ 100,00. Abra uma venda, adicione dois espetos e um
   refrigerante. Confira subtotal de R$ 35,00. Aplique R$ 5,00 de desconto:
   total R$ 30,00.
4. Registre PIX de R$ 10,00 e dinheiro de R$ 25,00. Conclua e confira troco
   de R$ 5,00, estoque 8/9 e caixa esperado R$ 120,00.
5. Abra outro rascunho, inclua/remova itens, altere quantidades e cancele.
   O estoque e o saldo financeiro não devem mudar.
6. Na consulta de vendas, cancele a primeira venda com motivo. O estoque
   deve voltar a 10/10 e o caixa esperado a R$ 100,00. Um segundo cancelamento
   não pode repor o estoque novamente.
7. Tente vender produto inativo, finalizar sem itens e vender mais que o
   estoque. Confira mensagens claras e ausência de alterações parciais.
8. Registre suprimento de R$ 20,00 e sangria de R$ 10,00. Confira esperado
   de R$ 110,00. Feche contando R$ 108,00 e confira diferença de -R$ 2,00.
9. Consulte vendas do dia, mais vendidos e estoque. Vendas canceladas não
   devem inflar os totais efetivos. Reabra o programa e confira persistência.
10. Em uma nova sessão, conclua outra venda e feche o caixa. Tentar cancelar
    essa venda após o fechamento deve ser bloqueado.

## Limitações conhecidas e próximos passos

- Login mock, sem autorização por perfil, criptografia do banco ou proteção
  contra alteração direta do arquivo por quem tem acesso ao computador.
- Operação local de um caixa; não há sincronização entre terminais/servidor.
- Consultas carregam o estado local em memória; paginação e consultas
  especializadas serão necessárias para históricos muito grandes.
- Sem NFC-e/SAT, TEF, confirmação automática de PIX, impressão de cupom,
  integração de balança, mesas/comandas, rodízio ou gorjetas.
- Sem devolução parcial após conclusão nem reabertura de caixa fechado.
  Antes da conclusão, remova o item; depois, cancele integralmente dentro das
  regras descritas acima.
- Sem instalador, atualização automática, exportação ou backup agendado.
- Execução gráfica deve ser validada em Windows; build em Linux não substitui
  teste de usabilidade no sistema de destino.

Próximas evoluções: autenticação e permissões, homologação fiscal, impressão,
venda por peso, comandas/mesas e rodízio, devoluções auditadas, backups
automatizados e relatórios gerenciais com exportação.
## Base web Blazor (.NET 8)

Aplicação Blazor Web App com interatividade no servidor, preparada para execução local em .NET 8. O projeto inclui o modelo inicial de domínio, persistência SQLite e dados de demonstração.

Esta base foi preservada na integração com `main`. Os módulos operacionais do
MVP acima pertencem ao WinForms e ainda não estão implementados na interface web.

### Estrutura do projeto web

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

### Tecnologias

- ASP.NET Core 8 Blazor Web App com Interactive Server e SignalR integrado.
- SQLite com Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`).
- QRCoder para a geração de QR Code.
- `System.Net.Sockets` e SignalR são fornecidos pelo .NET/ASP.NET Core, sem pacote adicional.

### Executar a base web

Na raiz do repositório:

```bash
dotnet run --project src/ChurrascariaPDV
```

O Kestrel escuta em `http://0.0.0.0:5000`, permitindo acesso por outros dispositivos na rede local em `http://<IP-LAN-DO-CAIXA>:5000`. O arquivo `churrascaria.db` é criado automaticamente na primeira inicialização com `EnsureCreated`.

O seed inicial contém 50 mesas, cinco categorias, os dez produtos solicitados e grupos/opções de modificadores para carnes e bebidas. Os preços são valores iniciais de demonstração e podem ser ajustados no banco de dados.

### Pacotes NuGet da base web

- `Microsoft.EntityFrameworkCore.Sqlite` 8.0.22
- `QRCoder` 1.6.0
