# Contribuindo

## Build e testes

```bash
dotnet build
dotnet test
```

## Estrutura

- `src/Parcelemais.Mcp.Tools/` — as ferramentas MCP em si (Orders/Simulations/Customers/Webhooks), independentes de transporte. Usa o SDK oficial `Twila.ParceleMais` por baixo.
- `src/Parcelemais.Mcp.Stdio/` — host local (stdio) pra Claude Desktop/Cursor. Credenciais fixas via variáveis de ambiente.
- `src/Parcelemais.Mcp.Http/` — host remoto (HTTP + OAuth2 Authorization Server) pro conector do Claude.ai e outras integrações remotas.
- `tests/Parcelemais.Mcp.Tests/` — testes unitários (criptografia, PKCE, formatação).

## Rodando o host HTTP localmente

```bash
export MCP_ENCRYPTION_KEY=$(openssl rand -hex 32)
export MCP_AUTO_CREATE_TABLES=1
export MCP_DYNAMODB_LOCAL_ENDPOINT=http://localhost:8000  # se estiver usando DynamoDB Local
dotnet run --project src/Parcelemais.Mcp.Http
```

Sem `MCP_DYNAMODB_LOCAL_ENDPOINT`, o host tenta se conectar ao DynamoDB real da AWS usando as credenciais padrão do ambiente (`~/.aws/credentials`, variáveis de ambiente, ou IAM role).

## Ao adicionar uma ferramenta nova

1. Verifique a API pública real do `Twila.ParceleMais` antes de escrever o wrapper — não invente nomes.
2. Siga o padrão dos arquivos existentes em `src/Parcelemais.Mcp.Tools/`: `[McpServerToolType]` na classe, `[McpServerTool(Name = "...")]` + `[Description]` no método e nos parâmetros, `try/catch (ParceleMaisException ex) { throw ex.ToMcpException(); }`.
3. Atualize a tabela de ferramentas no [README.md](README.md).

## Pull Requests

Este repositório é público — qualquer pessoa pode abrir um PR. Todo merge exige aprovação de alguém dos times Admin ou Backend da Twila Digital (`CODEOWNERS`).
