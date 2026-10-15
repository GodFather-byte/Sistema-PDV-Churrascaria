# Sistema PDV configurável

PDV (ponto de venda) e controle de mesas configurável por estabelecimento, rodando **100% local** no PC do caixa.
Os garçons lançam pedidos pelo celular conectado ao Wi-Fi do restaurante, sem internet externa.

- **Caixa** (`/caixa`): tela desktop widescreen com mapa de mesas em tempo real, recebimento e atalhos F1–F12.
- **Garçom** (`/garcom`): tela mobile touch-first com login por PIN, mapa de mesas, lançamento rápido e pré-conta.
- **Impressão por setor**: tickets ESC/POS via TCP 9100 para Bar/Copa, Cozinha, Churrasqueira e Caixa, com modo simulação.
- **Estoque** (`/admin/estoque`): entradas, perdas e histórico; baixa automática dos produtos controlados ao quitar a comanda.

## Executar

Pré-requisito: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Instalador para Windows (recomendado)

O GitHub Actions gera `PDV-Instalador-<versão>.exe` (workflow **Instalador**, em `.github/workflows/instalador.yml`):

- **Versão oficial:** crie e envie uma tag, por exemplo `git tag v1.0.0` e `git push origin v1.0.0`.
  O instalador fica anexado à Release da tag no GitHub.
- **Teste:** em *Actions > Instalador > Run workflow*; o `.exe` fica em *Artifacts* da execução.

O instalador já inclui o .NET (não precisa instalar nada antes), instala em `C:\PDV`, libera a porta 5000 no firewall
e cria os atalhos **PDV - Caixa** e **Configurar PDV**. Na primeira instalação, abre uma janela que pede tipo, nome,
gerente e PIN pessoal (sem PIN padrão). Para atualizar, execute o instalador de uma versão nova: o banco
(`estabelecimento.db`), a configuração e os backups são preservados e as migrations rodam ao iniciar.
Desinstalar também não apaga o banco nem os backups.

O Windows pode mostrar o aviso do SmartScreen porque o instalador não é assinado digitalmente:
clique em *Mais informações > Executar assim mesmo*.

### Instalar um estabelecimento novo pelo código-fonte (Windows)

Na raiz do repositório, execute uma vez `dotnet restore` e depois:

```powershell
.\scripts\novo-estabelecimento.ps1
```

Se o Windows bloquear scripts, revise a política com o responsável pelo computador.
Para permitir scripts locais apenas no terminal atual, pode-se usar
`Set-ExecutionPolicy -Scope Process RemoteSigned` antes do assistente, sem alterar a política permanente.

O assistente pergunta tipo, nome, pasta **nova**, nome do gerente e PIN pessoal com confirmação oculta.
Publica a aplicação, gera um `appsettings.json` exclusivo, aplica as migrations e o seed do perfil
e cria somente o gerente, sem PIN padrão. O PIN é enviado por entrada padrão e salvo apenas como hash.
Não há necessidade de instalar o `dotnet-ef` no caixa. Para iniciar, execute `iniciar.ps1` na pasta criada.
É necessário o runtime ASP.NET Core 8 no computador que executará a publicação.

Parâmetros opcionais (o PIN continua sendo solicitado de forma oculta):

```powershell
.\scripts\novo-estabelecimento.ps1 -Tipo Pizzaria -Nome "Pizza da Casa" -NomeGerente "Dono" -Destino "C:\PDV\Pizza" -Porta 5001
```

O destino existente é recusado: **nunca** use este assistente para atualizar um banco de produção.
Uma instalação incompleta mantém um marcador que impede iniciar sem concluir o gerente.
Confira o erro e use uma pasta nova; preserve a pasta incompleta para diagnóstico.
O seed também mantém um marcador de recuperação até sua transação terminar.
Uma instalação por pasta/banco; não copie o código para personalizar cada perfil.

Para desenvolvimento (sem gerente pré-configurado):

```bash
dotnet run --project src/ChurrascariaPDV
```

- Caixa: `http://localhost:5000/caixa`
- Garçons: `http://<IP-LAN-DO-CAIXA>:5000/garcom` (o endereço e o QR Code aparecem na tela inicial e no botão **Garçons (F11)** do caixa)

O banco `churrascaria.db` (SQLite) é criado e atualizado automaticamente via migrations na inicialização.
Se existir um banco da primeira versão (criado sem migrations), ele é renomeado para `churrascaria.db.legado-<data>.bak` e um novo é criado.

O perfil inicial é uma churrascaria com taxa de serviço de 10% opcional por comanda e ticket de 48 colunas.
Um gerente pode alterar o tipo e os dados do estabelecimento, a taxa e a largura do ticket em
**Perfil do estabelecimento** (`/admin/estabelecimento`).

### Painel do dono no celular (próxima etapa)

É possível implementar um painel PWA instalável com vendas, caixa, mesas e produtos mais vendidos.
Esse painel ficou **fora desta entrega**, por decisão de escopo.
O sistema atual usa Blazor Server: precisa de conexão com o servidor; não promete operação offline.
Para acesso fora do estabelecimento, planeje VPN ou proxy reverso com HTTPS, WebSockets, acesso restrito
e autenticação forte do proprietário (não apenas o PIN operacional de quatro dígitos).
Não encaminhe a porta HTTP do caixa diretamente para a internet. Nenhum túnel, certificado, hospedagem
ou publicação externa é criado pelo assistente.

### Liberar o acesso dos celulares (Windows)

Execute uma vez, como administrador, no PC do caixa:

```powershell
netsh advfirewall firewall add rule name="Churrascaria PDV" dir=in action=allow protocol=TCP localport=5000
```

### Publicar para o PC do caixa

```bash
dotnet publish src/ChurrascariaPDV -c Release -r win-x64 --self-contained -o publicacao
```

Copie a pasta `publicacao` para o PC do caixa junto com os arquivos de `scripts/`:
`liberar-firewall.bat` (executar uma vez como administrador) e `iniciar-caixa.bat`, que sobe o servidor e abre o caixa
em tela cheia. Também é possível executar `ChurrascariaPDV.exe` diretamente. Para abrir o caixa em tela cheia,
use o navegador em modo quiosque, por exemplo: `msedge --kiosk http://localhost:5000/caixa --edge-kiosk-type=fullscreen`.

## Licença (assinatura)

Cada instalação precisa de um arquivo de licença (`licenca.pdv`) emitido para o PC do caixa, com data de validade.
O arquivo é assinado digitalmente (ECDSA P-256) e conferido sem internet; editar o arquivo ou copiá-lo para outro PC invalida a licença.

- **Teste:** sem licença, o sistema funciona por 15 dias a partir da abertura do primeiro turno.
- **Vencimento:** aviso nos 7 dias anteriores; depois, 5 dias de carência; por fim, **somente leitura**:
  não abre turno nem lança pedidos, mas recebe contas abertas, fecha o caixa, mostra relatórios e mantém os backups.
- Atrasar o relógio do Windows não prolonga a licença (vale a data mais recente registrada nas vendas).
- O gerente vê o **código do computador** e instala o arquivo em **Gerência > Licença** (`/admin/licenca`).
- **Conferência online:** com internet, o caixa consulta o servidor de licenças ao iniciar (após 30 s), a cada 6 h
  e pelo botão **Conferir agora**. Recebe sozinho a licença renovada, ou o bloqueio feito no painel.
  Sem internet nada muda: o arquivo vale até a data de validade. As respostas são assinadas; um servidor falso não renova nem desbloqueia.
  Um caixa ainda sem arquivo recebe a licença cadastrada para o código da sua máquina.

### Servidor e painel de licenças (fornecedor)

O projeto `src/LicencasServidor` é o painel web do fornecedor: cadastrar clientes, emitir, renovar (+1 mês, +1 ano),
bloquear com motivo, baixar o `.pdv`, conferir um arquivo recebido, ver o último contato de cada caixa (data, versão, IP)
e gerenciar os acessos da equipe (senhas com 10+ caracteres, bloqueio após 5 erros). Ele também atende a API
`POST /api/v1/verificar`, usada pelos caixas. Não vai no instalador do PDV.

A **chave privada** fica fora do repositório (ex.: `C:\Users\<você>\Documents\PDV-Licencas\chave-privada.pem`) e no servidor.
Guarde cópias dela em local seguro: sem ela não é possível emitir nem renovar licenças; se vazar, qualquer um emite licenças.
A chave pública correspondente está em `ChavesLicenca` (`src/Licenciamento.Comum`). O servidor não inicia com uma chave que não corresponda a ela.

Configuração (variáveis de ambiente):

| Variável | Uso |
|---|---|
| `Licencas__ChavePrivada` | Conteúdo PEM da chave privada (ou `Licencas__ArquivoChavePrivada` com o caminho do arquivo) |
| `Admin__Usuario` / `Admin__Senha` | Cria o primeiro acesso ao painel, só se não houver nenhum. Remova a senha depois |
| `Dados__Pasta` | Pasta do banco e das chaves de sessão (no Docker: volume `/dados`) |

Rodar localmente: `dotnet run --project src/LicencasServidor` com as variáveis acima e abrir `http://localhost:5000`.

**Publicar** (qualquer hospedagem com Docker: VPS, Render, Railway, Fly.io):

```bash
docker build -f src/LicencasServidor/Dockerfile -t licencas-pdv .
docker run -d -p 8080:8080 -v licencas-dados:/dados \
  -e Licencas__ChavePrivada="$(cat chave-privada.pem)" -e Admin__Usuario=dono -e Admin__Senha='senha-forte-aqui' licencas-pdv
```

A hospedagem precisa fornecer **HTTPS** (o servidor confia no proxy para o IP e o protocolo) e um **volume persistente** em `/dados`;
sem ele, licenças e acessos se perdem a cada atualização. Depois de publicar, coloque o endereço em
`VerificacaoOnlineService.ServidorPadrao` (ex.: `https://licencas.seudominio.com.br`) e gere uma nova versão do instalador.
Até lá, a conferência online fica desativada e as licenças funcionam só por arquivo.
O endereço também pode ser definido por caixa em `Licenca:Servidor` no `appsettings.json`.

Se o cliente trocar de computador, o código muda: atualize o código da máquina no cadastro da licença.

> Como o código-fonte é público, a licença impede o uso casual sem pagamento, mas quem compilar o código
> pode remover a verificação. Para proteção mais forte, mantenha o repositório privado.

## Primeiro acesso e perfis de exemplo

Instalações novas **não têm usuários ou PINs padrão**. Use o assistente de instalação para criar o gerente
com PIN pessoal. O gerente cadastra a equipe em **Usuários e PINs** (`/admin/usuarios`).
Usuários novos ou com PIN redefinido precisam escolher um PIN diferente antes de operar.
Ao atualizar uma instalação existente, **todos os usuários** precisam trocar seus PINs no próximo acesso;
sessões antigas não são restauradas enquanto a troca estiver pendente. PINs passam a ser armazenados
como hashes com salt (PasswordHasher), não exibidos na administração.

O seed inicial é escolhido em `Instalacao:Tipo` e `Instalacao:Nome`, somente na criação do banco:
Churrascaria (cardápio original), Pizzaria (sabores e forno), Hamburgueria (adicionais e chapa),
Bar (petiscos e copa), RestaurantePorQuilo (buffet em kg) e Restaurante (prato do dia).
O peso é lançado como quantidade decimal em kg; não há integração automática com balança.
Produtos, categorias e setores são exemplos editáveis. Impressão começa simulada e sem IPs predefinidos.
Alterar o perfil de um banco existente **não reaplica seed nem apaga cadastros ou vendas**.
Cada instalação usa seu próprio banco; não há isolamento de múltiplos estabelecimentos no mesmo banco.

## Fluxo de operação

1. **Abrir o caixa** em *Turno e Sangria* (`/caixa/turno`) informando o fundo de troco. Sem turno aberto não é possível receber.
2. **Garçom** entra com o PIN, toca na mesa e lança os itens. A mesa livre abre sozinha no primeiro pedido (fica vermelha) e registra o horário.
   - Carnes pedem o *Ponto da Carne* (obrigatório); bebidas oferecem *Com Gelo e Limão*, *Sem Gelo*, *2 Copos*.
   - **Enviar Pedido para Produção** separa os itens por setor: rodízio vai só para a conta, bebidas para o Bar/Copa,
     porções para a Cozinha e cortes para a Churrasqueira (com o ponto em MAIÚSCULAS).
   - **+ Comanda** cria uma comanda individual na mesa para quem quer pagar separado.
3. **Pedir Fechamento / Pré-Conta**: a mesa fica amarela piscando e a conferência é impressa no caixa.
4. **Caixa** seleciona a mesa (clique ou digite o número + Enter), confere os itens, aplica a taxa de serviço configurada e recebe:
   - pagamento parcial ou total em Dinheiro (com troco), Pix, Cartão Crédito ou Débito;
   - divisão automática por pessoa (“R$ 440,00 / 4 = R$ 110,00”), com botão para usar o valor por pessoa;
   - quando o saldo chega a R$ 0,00 a mesa vai para *Limpeza* (azul/cinza); depois, **Mesa limpa · Liberar** (F10).
5. **Fechar o caixa** no fim do dia: o sistema mostra o resumo por forma de pagamento, o dinheiro esperado na gaveta
   e a diferença para o valor contado, e imprime o fechamento.

### Atalhos do caixa

| Tecla | Ação | Tecla | Ação |
|-------|------|-------|------|
| `0-9` + `Enter` | Selecionar mesa | `F7` | Ativar/desativar taxa de serviço (quando opcional) |
| `F1` | Ajuda | `F8` | Cancelar item (PIN do gerente) |
| `F2` | Lançar item | `F9` | Receber pagamento |
| `F3` | Transferir mesa | `F10` | Finalizar / liberar mesa |
| `F4` | Transferir item | `F11` | QR Code dos garçons |
| `F5` | Atualizar | `F12` | Nova comanda |
| `F6` | Imprimir pré-conta | `Esc` | Fechar janela / limpar seleção |

## Regras de negócio

- **Abertura automática** da mesa no primeiro item; grupos com **Sugerir pessoas** usam a quantidade para sugerir o número de pessoas.
  O perfil inicial associa essa regra aos rodízios existentes.
- **Conta geral ou comandas individuais** na mesma mesa; comanda individual quitada é encerrada sem liberar a mesa.
- **Comanda individual vazia** (sem itens lançados e sem pagamentos) pode ser cancelada pelo caixa ou pelo garçom:
  o número do cartão volta a ficar livre e a mesa só é liberada quando não há outras comandas abertas nem consumo anterior.
- **Transferência de mesa**: leva todas as comandas; se o destino estiver ocupado as contas são juntadas.
  A origem fica aguardando limpeza.
- **Transferência de item** (total ou parcial) para outra comanda, da mesma mesa ou de outra.
- **Cancelamento** (total ou parcial) exige PIN de gerente e motivo; o setor recebe um ticket “NÃO PREPARAR”.
  Não é permitido cancelar se os pagamentos já feitos passarem a ser maiores que o consumo.
- **Taxa de serviço** configurável no perfil do estabelecimento e opcional por comanda quando habilitado; fica registrada em cada pagamento para o relatório.
- **Estorno de pagamento** com PIN de gerente, enquanto a conta e o turno estiverem abertos. O gerente que autorizou,
  o motivo e quem recebeu aparecem em **Relatórios > Estornos de pagamento**.
- **Tentativas de PIN limitadas por aparelho**: após 5 PINs errados seguidos (login, troca de PIN ou PIN de gerente),
  o aparelho fica bloqueado por 30 s, dobrando a cada novo erro até 15 min. Um PIN correto zera a contagem.
- **Backup automático**: ao iniciar e a cada 24 h o banco é copiado para `backups/` (ao lado do banco), mantendo as 14 cópias
  mais recentes. Ajuste com `Backup:Pasta` e `Backup:Retencao` no `appsettings.json`. Copie essa pasta para um pendrive ou nuvem
  periodicamente; um backup no mesmo disco não protege contra falha do PC.
- Novo item depois da pré-conta devolve a mesa para *Ocupada*.
- **Estoque de produtos prontos**: ative o controle no cadastro do produto e lance entradas/saídas em Estoque.
  A venda baixa a quantidade quando a comanda é quitada; itens cancelados são registrados como perda.
  O saldo pode ficar negativo e aparece sinalizado para conferência. Produtos sem controle de estoque não são afetados.

## Impressoras

Cadastre e edite os setores em **Gerência > Setores de impressão** (`/admin/setores`):
nome, IP, porta, habilitado e destaque dos itens/observações. Escolha um setor para receber pré-contas e
fechamentos. O cadastro do produto permite selecionar qualquer setor ou **Sem impressão**.

Bar / Copa, Cozinha, Churrasqueira e Caixa são preservados como setores iniciais.
Na primeira inicialização após a atualização, a configuração antiga `Impressao:Impressoras` é importada
para o banco uma única vez; mudanças posteriores devem ser feitas pela tela, sem editar código ou JSON.
Setores desabilitados não enviam bytes para a rede e seus tickets ficam no histórico como simulados.

Somente o modo de simulação e o timeout continuam em `src/ChurrascariaPDV/appsettings.json`:

```json
"Impressao": {
  "ModoSimulacao": true,
  "TimeoutMs": 3000
}
```

Com `ModoSimulacao: true` nada é enviado à rede: os tickets aparecem no console e na tela **Impressões** (`/impressoes`).
Com `false`, os tickets são enviados em ESC/POS (code page PC860, corte parcial) por `TcpClient`. Se uma impressora falhar,
o pedido continua salvo, o item aparece como “não impresso” no caixa e pode ser reimpresso.
O sistema também tenta de novo sozinho após 15 s, 45 s e 2 min, enviando só para os setores que falharam
(os que já imprimiram não recebem cópia). Se der certo, o aviso “não impresso” some.

## Modificadores e tipos de estabelecimento

### Sabores e meio a meio

Cadastre um grupo de opções marcado como **Grupo de sabores**, obrigatório e com múltipla escolha.
Cada linha usa `Nome | preço`, por exemplo `Calabresa | 50,00` e `Especial | 70,00`.
Vincule apenas um grupo de sabores por produto e configure nele o limite (1–8) e a regra:
**Maior valor**, **Média** simples ou **Soma** dos sabores escolhidos.
As partes são iguais: duas escolhas representam meio a meio. O resultado substitui o preço-base;
opções dos outros grupos (borda, extras) são somadas como adicionais, uma vez por unidade.
O carrinho mostra o mesmo cálculo validado novamente no servidor. Preço e sabores ficam registrados
no pedido para não alterar vendas anteriores quando o cardápio for editado.

Em **Cardápio** (`/admin/produtos`), cada grupo permite configurar:

- escolha obrigatória ou opcional e seleção única ou múltipla;
- setor de produção que substitui o setor do produto quando o grupo é aplicado;
- destaque das observações na produção;
- uso da quantidade do item para sugerir pessoas da mesa.

Grupos com opções aplicam suas regras quando uma opção é selecionada. Grupos sem opções aplicam
a regra sempre, sem abrir uma janela de escolha; não podem ser obrigatórios.
Se as escolhas direcionarem o mesmo item para setores diferentes, o pedido é rejeitado com uma mensagem.
Os grupos continuam vinculados explicitamente aos produtos: um bar sem rodízio ou uma pizzaria sem ponto
de carne não precisam cadastrar nem usar esses grupos. O tipo **Produto** é a classificação padrão;
os tipos antigos continuam disponíveis, mas não impõem regras nem setores.

A churrascaria mantém seus grupos de ponto da carne e preferência de bebida. A atualização cria um
grupo de consumo por pessoa para os rodízios existentes, sem sobrescrever grupos personalizados.
Setor e destaque são registrados no item ao lançar o pedido e preservados nas transferências e nos
cancelamentos parciais. Alterações futuras no produto ou grupo não redirecionam reimpressões antigas.

## Tempo real

Todas as telas abertas (caixa e celulares) são atualizadas na hora quando uma mesa abre, recebe pedido, pede a conta
ou é paga. As telas Blazor usam a conexão SignalR do próprio Blazor Server; integrações externas podem assinar o hub
`/hubs/mesas` (evento `MesaAtualizada`).

## Estrutura

```text
ChurrascariaPDV.sln
├── src/ChurrascariaPDV/
│   ├── Components/
│   │   ├── Layout/            # MainLayout (gerência), TelaCheiaLayout (caixa/garçom), NavMenu
│   │   ├── Pages/             # Caixa, Garcom, CaixaTurno, Relatorios, Impressoes, Home, Admin/*
│   │   └── Shared/            # MapaMesas, SeletorProdutos, TecladoPin, AreaProtegida, Modal...
│   ├── Domain/                # Entidades e enums
│   ├── Infrastructure/Data/   # AppDbContext, seed, migrations, preparação do banco
│   ├── Services/              # MesaService, PedidoService, PagamentoService, CaixaService,
│   │   │                      # CardapioService, EstoqueService, UsuarioService, RelatorioService
│   │   ├── Impressao/         # Documento ESC/POS, layouts dos tickets, envio TCP 9100
│   │   └── Notificacoes/      # Notificador de mesas + hub SignalR
│   └── wwwroot/               # app.css (tema), js/pdv.js (atalhos), bootstrap
└── tests/ChurrascariaPDV.Tests/   # Testes xUnit das regras de negócio (SQLite em memória)
```

## Testes

```bash
dotnet test
```

## Desenvolvimento

Para alterar o modelo de dados, gere uma nova migration:

```bash
dotnet tool install -g dotnet-ef --version 8.0.22
dotnet ef migrations add NomeDaMudanca --project src/ChurrascariaPDV --output-dir Infrastructure/Data/Migrations
```

## Tecnologias

- ASP.NET Core 8 Blazor Web App (Interactive Server) com SignalR
- Entity Framework Core 8 + SQLite (modo WAL)
- QRCoder para o QR Code de acesso dos garçons
- `System.Net.Sockets.TcpClient` para impressão ESC/POS
- xUnit para testes
