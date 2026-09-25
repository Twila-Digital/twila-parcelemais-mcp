<p align="center">
  <img src="https://raw.githubusercontent.com/Twila-Digital/twila-parcelemais-go-sdk/production/assets/logo-light.svg" alt="Parcele+" width="180" style="max-width: 100%;">
</p>

# Parcele+ MCP Server

Servidor [MCP](https://modelcontextprotocol.io) para usar a API do [Parcele+](https://www.cartaosimples.com.br) no **Claude Desktop**, no **Cursor** ou por **URL remota** (Claude.ai, automações, outras integrações).

**Conteúdo:** [Início rápido](#início-rápido) · [Como rodar o servidor](#como-rodar-o-servidor) · [Conectar via OAuth (Claude.ai / remoto)](#conectar-via-oauth-claudeai--remoto) · [Ferramentas](#ferramentas) · [Problemas comuns](#problemas-comuns)

---

## Início rápido

**Pré-requisito:** [.NET 8 SDK](https://dotnet.microsoft.com/download) ou superior.

```bash
git clone https://github.com/Twila-Digital/twila-parcelemais-mcp.git
cd twila-parcelemais-mcp
dotnet build
```

**Credenciais:** peça seu `ClientId`/`ClientSecret` do Parcele+ ao time de integração. Use as de **staging** pra testar sem mexer com dinheiro real.

**Claude Desktop / Cursor** (modo local, processo próprio, via stdio):

```json
{
  "mcpServers": {
    "parcelemais": {
      "command": "dotnet",
      "args": ["run", "--project", "/CAMINHO/absoluto/para/twila-parcelemais-mcp/src/Parcelemais.Mcp.Stdio"],
      "env": {
        "PARCELEMAIS_CLIENT_ID": "seu-client-id",
        "PARCELEMAIS_CLIENT_SECRET": "seu-client-secret",
        "PARCELEMAIS_ENVIRONMENT": "Staging"
      }
    }
  }
}
```

`PARCELEMAIS_ENVIRONMENT` aceita `Staging` ou `Production` (padrão: `Production` — defina explicitamente pra staging durante testes).

---

## Como rodar o servidor

Escolha **uma** opção.

### No seu computador com Cursor ou Claude Desktop (o mais simples)

O próprio app **liga** o servidor pra você via stdio (`dotnet run --project src/Parcelemais.Mcp.Stdio`). Você só configura o caminho e as variáveis de ambiente, como no [início rápido](#início-rápido). Não precisa de URL, porta ou OAuth.

### Na internet (HTTP) — Claude.ai, n8n, automações

Use o endpoint público quando você integra com Claude.ai (conector remoto), n8n, outras automações, ou quer configuração remota no cliente (sem `command`/processo local).

Esse endpoint aceita dois modos de autenticação:

1. **`Authorization: Bearer <token>`** (ou header `X-API-Key`) — o token é obtido via OAuth (veja abaixo).
2. **OAuth 2.0** — obrigatório pro **conector remoto do Claude.ai**, que não aceita headers estáticos e exige um fluxo de autorização.

Exemplo de configuração remota (Cursor, com header direto — o token vem do fluxo OAuth):

```json
{
  "mcpServers": {
    "parcelemais": {
      "url": "https://<seu-deploy>/mcp",
      "headers": {
        "Authorization": "Bearer TOKEN_OBTIDO_VIA_OAUTH"
      }
    }
  }
}
```

### Local, servindo HTTP (pra testar OAuth)

```bash
export MCP_ENCRYPTION_KEY=$(openssl rand -hex 32)
export MCP_AUTO_CREATE_TABLES=1          # cria as tabelas DynamoDB locais/de teste se não existirem
export MCP_DYNAMODB_LOCAL_ENDPOINT=http://localhost:8000  # opcional, pra DynamoDB Local
dotnet run --project src/Parcelemais.Mcp.Http
# porta padrão via ASPNETCORE_URLS, ex.: http://localhost:5000
```

---

## Conectar via OAuth (Claude.ai / remoto)

O servidor implementa um Authorization Server OAuth 2.0 completo (Dynamic Client Registration + Authorization Code + PKCE), necessário porque **o conector remoto do Claude.ai exige OAuth** — não é possível conectar apenas com um header estático nesse cliente.

Fluxo, do ponto de vista do Claude/cliente MCP:

1. O cliente descobre os metadados em `/.well-known/oauth-protected-resource` e `/.well-known/oauth-authorization-server`.
2. Registra-se dinamicamente em `POST /register` (RFC 7591).
3. Abre `GET /authorize` no navegador do usuário — uma página simples pede o **Client ID**, **Client Secret** e o **ambiente** (staging/produção) do Parcele+ (não uma senha de conta).
4. Após validar as credenciais contra a API do Parcele+ (gerando um token de acesso real), o servidor emite um código de autorização e redireciona de volta ao cliente.
5. O cliente troca o código por um token em `POST /token` (com verificação PKCE). O "token" retornado é, na prática, a própria credencial (Client ID + Client Secret + ambiente), guardada de forma criptografada (AES-256-GCM) até esse ponto.
6. O cliente usa esse token como `Authorization: Bearer` normalmente em `/mcp` — o SDK oficial `Twila.ParceleMais` cuida de gerar/renovar o token real de acesso à API por trás dos panos, por credencial.

Não há conta de usuário nem senha do Parcele+ envolvida além do Client ID/Secret do lojista — o mesmo modelo de autenticação usado no header direto.

Storage do authorization server (clients registrados via DCR + códigos de autorização de curta duração) é DynamoDB — ver `deploy/` pra provisionamento.

---

## Ferramentas

Nomes exatos das tools são os registrados no código (`src/Parcelemais.Mcp.Tools/`); a lista completa aparece no cliente MCP ao conectar. Resumo por recurso:

| Recurso | Tools |
|---|---|
| Pedidos (Orders) | `createOrder`, `getOrder`, `listOrders`, `startCdcSale`, `importOrderInvoice` |
| Simulações | `simulateInstallments`, `simulateValues` |
| Clientes | `getCustomer`, `listCustomers` |
| Lojas da rede (Establishments) | `createEstablishment`, `getEstablishment`, `listEstablishments`, `updateEstablishment`, `updateEstablishmentBankAccount`, `activateEstablishment`, `deactivateEstablishment` |
| Webhooks | `createWebhook`, `listWebhooks`, `updateWebhook`, `deleteWebhook` |

**Notas importantes de negócio, refletidas nas ferramentas:**
- Valores são sempre em reais (`1500.00`), não centavos.
- `startCdcSale` só faz sentido com o pedido `Approved` — chamar antes disso retorna erro da API.
- `createWebhook` retorna a chave de assinatura **uma única vez** — guarde-a com segurança pra validar eventos recebidos.
- `updateEstablishment` não altera a razão social nem a conta bancária — pra trocar a conta use `updateEstablishmentBankAccount` (substitui a conta inteira). Loja inativa não aceita novos pedidos.
- No modelo de desembolso `External`, nome e CPF/CNPJ do titular da conta são obrigatórios.
- `updateWebhook`/`deleteWebhook` são por tipo (`Customer`/`Simulation`/`Order`) — só existe um webhook cadastrado por tipo.

## Ideias de prompts

- Simulação: *"Simule as parcelas de um valor de R$ 1.500 pro Parcele+."*
- Pedido: *"Crie um pedido pro cliente João Silva (CPF 12345678900) no valor de R$ 1.500 e me dê o link de pagamento assim que aprovado."*
- Conferir vendas: *"Liste os pedidos aprovados dos últimos 7 dias."*
- Lojas: *"Liste as lojas ativas da minha rede."*
- Webhook: *"Cadastre um webhook de pedidos apontando pra https://meusite.com/webhooks/parcelemais."*

---

## Problemas comuns

| Situação | O que verificar |
|----------|------------------|
| Erro de credencial | Cursor/Claude local: `PARCELEMAIS_CLIENT_ID`/`PARCELEMAIS_CLIENT_SECRET` no `env` da config. HTTP: conecte via OAuth (o header estático exige um token já emitido pelo fluxo). |
| MCP não conecta (local) | Caminho absoluto pro projeto `Parcelemais.Mcp.Stdio`, .NET 8 SDK instalado, reiniciar o app após mudar a config. |
| Claude.ai não conecta (remoto) | Confirme que está usando o fluxo OAuth (o conector do Claude.ai exige `/authorize`); não funciona apenas com header estático nesse cliente específico. |
| 401 numa tool | O Client ID/Secret usados no `/authorize` precisam ser válidos no ambiente selecionado (staging ≠ produção). |

---

## Contribuição e licença

- Contribuição: [CONTRIBUTING.md](CONTRIBUTING.md)
- Licença: [LICENSE](LICENSE) (MIT)
