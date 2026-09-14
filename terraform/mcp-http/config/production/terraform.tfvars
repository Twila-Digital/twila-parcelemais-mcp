#################################################
#               Common Variables               #
#################################################
name        = "parcelemais-mcp-http"
environment = "production"
region      = "us-east-1"

#################################################
#         Shared infra (looked up by name)      #
#################################################
# Real IDs (VPC, subnets, ALB security group) are never committed to this
# public repository — they're derived via data source from the names below,
# or supplied privately. See README.md.
shared_ecs_cluster_name = "production-cartaosimples"
shared_alb_name         = "production-cartaosimples-backend"

health_check_path    = "/health"
health_check_matcher = "200"

# TODO (decisão humana, não preencher às cegas):
# - alb_ingress_unauthenticated_hosts: qual subdomínio aponta pra este serviço (ex.: mcp.parcelemais.com.br)?
#   Precisa existir o registro DNS correspondente antes do apply — fora do escopo deste módulo.
# - alb_ingress_unauthenticated_listener_arns: confirme o listener ARN certo do ALB compartilhado.
# - alb_ingress_listener_unauthenticated_priority: cs-webintegration já usa a prioridade 3 nesse
#   listener — 4000 abaixo é só um placeholder, confira as regras já existentes antes de aplicar.
alb_ingress_unauthenticated_hosts             = ["mcp.parcelemais.com.br"] # TODO: confirmar host real
alb_ingress_unauthenticated_listener_arns     = []                         # TODO: preencher
alb_ingress_listener_unauthenticated_priority = 4000                       # TODO: confirmar prioridade livre

#################################################
#                  Application                  #
#################################################
container_cpu                = 256
container_memory             = 512
container_memory_reservation = 256
container_port               = 8080

desired_count            = 1
ecs_alarms_enabled       = true
autoscaling_min_capacity = 1
autoscaling_max_capacity = 2

cloudwatch_log_group_enabled = true

dynamodb_table_prefix = "ParcelemaisMcp"

# `ecs_private_subnet_ids` NÃO é definido aqui de propósito — é um dado de infra real
# (subnet IDs da VPC compartilhada) e fica fora deste repositório público. Aplique com
# um segundo -var-file apontando pro valor privado (ver README.md).
