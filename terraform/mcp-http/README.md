# Terraform — MCP HTTP server (ECS Fargate + ALB)

Provisiona o host HTTP remoto do servidor MCP (`src/Parcelemais.Mcp.Http`) em ECS Fargate, atrás do ALB compartilhado já usado pelos demais serviços (mesmo padrão do `cs-webintegration` no `twila-cartaosimples-backend`), com as duas tabelas DynamoDB do authorization server OAuth.

Não usa AWS App Runner — o serviço está [fechando pra novos clientes a partir de 30/04/2026](https://aws.amazon.com/pt/apprunner/), com a própria AWS recomendando ECS como substituto.

## Estrutura

- `context.tf` / `variables.tf` / `outputs.tf` — padrão `null-label` do resto do `twila-terraform`.
- `main.tf` — provisiona:
  - `module.ecs_web_app` (`./modules/ecs-web-app`) — ECS Fargate + ECR + CloudWatch Logs.
  - `module.alb_ingress` (`./modules/alb-ingress`) — listener rule no ALB compartilhado, **por host** (não por path — o app serve vários paths de topo: `/mcp`, `/authorize`, `/token`, `/register`, `/revoke`, `/.well-known/*`, `/health`, e o módulo só aceita 1 path pattern por regra).
  - `module.oauth_clients_table` / `module.auth_codes_table` (`./modules/dynamodb-table`) — storage do authorization server. `label_order = ["name"]` garante que o nome físico da tabela seja exatamente `${var.dynamodb_table_prefix}OAuthClients`/`AuthCodes`, sem prefixo namespace-environment-stage — o app constrói esses nomes literalmente a partir de `MCP_DYNAMODB_TABLE_PREFIX`.
  - `aws_secretsmanager_secret.mcp_encryption_key` — só o container do segredo; **o valor não é definido pelo Terraform** (ver abaixo).
  - `aws_iam_policy.task_permissions` — acesso do task role às 2 tabelas + ao segredo.
- `modules/` — **não versionado neste repositório**, populado pelo mesmo processo externo usado pelo `cs-webintegration` (vendoring de `twila-terraform`). Precisa conter `label/`, `ecs-web-app/`, `alb-ingress/`, `dynamodb-table/` antes de rodar `terraform init`.
- `config/{production,staging}/terraform.tfvars` — só valores não sensíveis: nomes (`shared_ecs_cluster_name`, `shared_alb_name`), sizing, autoscaling. **Nenhum ID de conta/VPC/subnet/ALB é commitado neste repositório público** — `vpc_id`, `ecs_cluster_arn`, `alb_arn_suffix` e o security group do ALB são resolvidos em `main.tf` via `data source` (busca por nome), não por literal.
- `config/{production,staging}/backend.hcl.example` — mostra o formato do backend S3, sem o bucket real (que embute o account ID). O `backend.hcl` de verdade, e as subnet IDs privadas reais (`ecs_private_subnet_ids` — não há como derivá-las com segurança via `data source` sem antes confirmar a convenção de tag usada na conta), ficam no repositório privado [`twila-terraform`](https://github.com/Twila-Digital/twila-terraform), em `deployments/parcelemais-mcp-http/{production,staging}/`.

## O que falta preencher antes do primeiro `apply`

Os seguintes valores em `config/<ambiente>/terraform.tfvars` **precisam de decisão humana** antes do apply (deixados como placeholder, não inventados):

- `alb_ingress_unauthenticated_hosts` — qual subdomínio/host vai rotear pro MCP (ex.: `mcp.parcelemais.com.br` / `mcp.staging.parcelemais.com.br`)? Depende de criar o registro DNS correspondente (fora do escopo deste módulo).
- `alb_ingress_unauthenticated_listener_arns` — o listener ARN do ALB compartilhado (o mesmo `cs-webintegration` usa, mas confirme).
- `alb_ingress_listener_unauthenticated_priority` — uma prioridade **livre** nesse listener (o `cs-webintegration` já usa `3`; escolhi `4000` como placeholder, mas confira as regras já existentes no ALB antes de aplicar, pra não colidir).

## Aplicando (combina o repo público com o privado)

```bash
TERRAFORM_PRIVATE=/caminho/pra/twila-terraform/deployments/parcelemais-mcp-http/production

terraform init -backend-config="$TERRAFORM_PRIVATE/backend.hcl"

terraform apply \
  -var-file=config/production/terraform.tfvars \
  -var-file="$TERRAFORM_PRIVATE/subnets.secrets.tfvars"
```

Troque `production` por `staging` conforme o ambiente.

## Depois do `terraform apply`

1. **Popule o segredo de criptografia** (o Terraform só cria o container do segredo, nunca o valor):
   ```bash
   aws secretsmanager put-secret-value \
     --secret-id "$(terraform output -raw mcp_encryption_key_secret_arn)" \
     --secret-string "$(openssl rand -hex 32)"
   ```

2. **Publique a imagem** no ECR criado (build a partir da raiz do repositório, já que o `Dockerfile` faz `COPY global.json Directory.Build.props ./` e `COPY src/`):
   ```bash
   REPO_URL=$(terraform output -raw ecr_repository_url)
   aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin "$REPO_URL"
   docker build -f src/Parcelemais.Mcp.Http/Dockerfile -t "$REPO_URL:latest" .
   docker push "$REPO_URL:latest"
   ```

3. Force um novo deployment do serviço ECS pra pegar a imagem recém-publicada (`aws ecs update-service --force-new-deployment ...`), já que a primeira revisão da task definition provavelmente sobe antes de haver qualquer imagem publicada.

## Rodando localmente (validação, sem aplicar)

```bash
terraform init -backend=false   # requer modules/ populado
terraform validate
```
