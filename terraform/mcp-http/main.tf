provider "aws" {
  region = var.region
}

terraform {
  backend "s3" {}
}

data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}

# ---------------------------------------------------------------------------
# Shared infra, looked up by NAME instead of hardcoded IDs/ARNs — this file is
# committed to a public repository, so no raw account ID, VPC ID, or ALB/SG ID
# should ever appear here. Only human-readable names (supplied via
# `shared_ecs_cluster_name`/`shared_alb_name`, populated from the private
# infra config — see README.md) are needed.
# ---------------------------------------------------------------------------

data "aws_ecs_cluster" "shared" {
  cluster_name = var.shared_ecs_cluster_name
}

data "aws_lb" "shared" {
  name = var.shared_alb_name
}

locals {
  # "arn:...:loadbalancer/app/name/id" -> "app/name/id", the format CloudWatch
  # dimensions and the alb-target-group-cloudwatch-sns-alarms module expect.
  alb_arn_suffix = element(split("loadbalancer/", data.aws_lb.shared.arn), 1)
}

# ---------------------------------------------------------------------------
# OAuth authorization server storage (DynamoDB) — see
# src/Parcelemais.Mcp.Http/Storage/DynamoDbOAuthStore.cs for the exact schema
# this mirrors. Table names must equal `${var.dynamodb_table_prefix}OAuthClients`
# / `${var.dynamodb_table_prefix}AuthCodes` — the app builds those names itself
# from MCP_DYNAMODB_TABLE_PREFIX, they are NOT derived from `module.this.id`.
# ---------------------------------------------------------------------------

module "oauth_clients_table" {
  source = "./modules/dynamodb-table"

  hash_key = "ClientId"

  # label_order = ["name"] forces module.this.id (used as the table name below) to be exactly
  # `var.name`, with no namespace/environment/stage prefix — the app builds table names itself as
  # literally "${MCP_DYNAMODB_TABLE_PREFIX}OAuthClients", so the two must match exactly.
  label_order = ["name"]
  context     = module.this.context
  name        = "${var.dynamodb_table_prefix}OAuthClients"
}

module "auth_codes_table" {
  source = "./modules/dynamodb-table"

  hash_key           = "Code"
  ttl_attribute_name = "ExpiresAt"

  label_order = ["name"]
  context     = module.this.context
  name        = "${var.dynamodb_table_prefix}AuthCodes"
}

# ---------------------------------------------------------------------------
# Encryption key for credentials at rest (AES-256-GCM, MCP_ENCRYPTION_KEY).
# The secret VALUE is intentionally not set here — populate it out-of-band
# after apply (see README.md). Terraform only creates the secret container.
# ---------------------------------------------------------------------------

resource "aws_secretsmanager_secret" "mcp_encryption_key" {
  name = "${module.this.id}-encryption-key"
  tags = module.this.tags
}

# ---------------------------------------------------------------------------
# IAM: let the ECS task read/write both tables and read the encryption secret
# ---------------------------------------------------------------------------

data "aws_iam_policy_document" "task_permissions" {
  statement {
    sid    = "OAuthStorageAccess"
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:DeleteItem",
      "dynamodb:Query",
    ]
    resources = [
      module.oauth_clients_table.table_arn,
      module.auth_codes_table.table_arn,
    ]
  }

  statement {
    sid       = "EncryptionKeyAccess"
    effect    = "Allow"
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.mcp_encryption_key.arn]
  }
}

resource "aws_iam_policy" "task_permissions" {
  name   = "${module.this.id}-task-permissions"
  policy = data.aws_iam_policy_document.task_permissions.json
  tags   = module.this.tags
}

# ---------------------------------------------------------------------------
# ALB routing — host-based, not path-based. The app serves several top-level
# paths (/mcp, /authorize, /token, /register, /revoke, /.well-known/*,
# /health) and `alb-ingress` only supports a single path pattern per rule, so
# a dedicated host avoids that constraint and keeps this service independent
# of whatever else shares the ALB. See variables.tf for
# `alb_ingress_unauthenticated_hosts`.
# ---------------------------------------------------------------------------

module "ecs_web_app" {
  source = "./modules/ecs-web-app"

  region = var.region
  vpc_id = data.aws_lb.shared.vpc_id

  ecr_enabled   = true
  use_ecr_image = true

  task_cpu    = var.task_cpu
  task_memory = var.task_memory

  container_cpu                = var.container_cpu
  container_memory             = var.container_memory
  container_memory_reservation = var.container_memory_reservation
  container_port               = var.container_port
  port_mappings = [
    {
      containerPort = var.container_port
      hostPort      = var.container_port
      protocol      = "tcp"
    }
  ]
  aws_logs_region = var.region

  map_container_environment = {
    MCP_DYNAMODB_TABLE_PREFIX = var.dynamodb_table_prefix
    ASPNETCORE_URLS           = "http://+:${var.container_port}"
  }

  secrets = [
    {
      name      = "MCP_ENCRYPTION_KEY"
      valueFrom = aws_secretsmanager_secret.mcp_encryption_key.arn
    }
  ]

  ecs_private_subnet_ids = var.ecs_private_subnet_ids
  ecs_cluster_arn        = data.aws_ecs_cluster.shared.arn
  ecs_cluster_name       = var.shared_ecs_cluster_name
  desired_count          = var.desired_count

  alb_arn_suffix               = local.alb_arn_suffix
  alb_security_group           = tolist(data.aws_lb.shared.security_groups)[0]
  alb_ingress_protocol_version = var.alb_ingress_protocol_version
  alb_ingress_target_group_arn = module.alb_ingress.target_group_arn

  autoscaling_min_capacity = var.autoscaling_min_capacity
  autoscaling_max_capacity = var.autoscaling_max_capacity

  ecs_alarms_enabled           = var.ecs_alarms_enabled
  cloudwatch_log_group_enabled = var.cloudwatch_log_group_enabled

  task_policy_arns = [aws_iam_policy.task_permissions.arn]

  context = module.this.context
}

module "alb_ingress" {
  source = "./modules/alb-ingress"

  vpc_id               = data.aws_lb.shared.vpc_id
  protocol_version     = var.alb_ingress_protocol_version
  health_check_path    = var.health_check_path
  health_check_matcher = var.health_check_matcher
  port                 = var.container_port

  unauthenticated_listener_arns = var.alb_ingress_unauthenticated_listener_arns
  unauthenticated_hosts         = var.alb_ingress_unauthenticated_hosts
  unauthenticated_priority      = var.alb_ingress_listener_unauthenticated_priority

  context = module.this.context
}
