# Instruções do projeto

## Contexto

- Sistema de ponto de venda para restaurantes, executado localmente no caixa e acessado pelos garçons pela rede local.
- Aplicação ASP.NET Core com Blazor Server em .NET 8; persistência com Entity Framework Core e SQLite.
- A lógica de negócio fica principalmente em `src/ChurrascariaPDV/Services/`; entidades e enums ficam em `Domain/`; interface em `Components/`; persistência, migrations e seed em `Infrastructure/`.
- O sistema não promete operação offline nem acesso seguro pela internet. Não exponha o servidor diretamente à internet nem enfraqueça autenticação ou controles de acesso.

## Como trabalhar

- Antes de alterar código, localize a lógica existente e reutilize os serviços, componentes, validações e padrões do projeto.
- Preserve os fluxos de caixa, comandas, pagamentos, estoque, impressão e primeiro acesso; mudanças nesses fluxos devem ser compatíveis com instalações e dados existentes.
- Não inclua PINs padrão, credenciais ou dados sensíveis em código, logs, testes ou documentação. PINs são pessoais e armazenados como hash.
- Não execute scripts de instalação, migrations ou operações contra o banco de dados real como parte de testes. Use os testes existentes e bancos isolados.
- Crie ou altere migrations do EF Core somente quando uma mudança de modelo exigir; nunca apague ou recrie dados de produção para validar uma alteração.
- Mantenha textos e mensagens da interface em português e siga o estilo já usado nos arquivos próximos.

## Build e testes

Na raiz da solução:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release
```

Para validar uma mudança, prefira executar primeiro os testes relacionados e a compilação. Os testes ficam em `tests/ChurrascariaPDV.Tests/`.

## Uso de outros agentes

- Delegue somente tarefas independentes que se beneficiem de investigação em contexto separado, com objetivo, escopo e resultado esperado bem definidos.
- Não delegue uma única investigação contínua que atravesse arquivos ou subsistemas; siga o fluxo diretamente para manter o contexto.
- Para pesquisa ou revisão, prefira delegar uma análise somente de leitura. Não peça a vários agentes para editar os mesmos arquivos.
- Integre e confira os resultados delegados no código e nos testes antes de concluir; uma recomendação de agente não substitui validação.
