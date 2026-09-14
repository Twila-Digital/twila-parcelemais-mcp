output "ecr_repository_url" {
  value       = module.ecs_web_app.ecr_repository_url
  description = "Push container images here (see README.md)"
}

output "ecs_service_name" {
  value       = module.ecs_web_app.ecs_service_name
  description = "ECS service name"
}

output "oauth_clients_table_name" {
  value       = module.oauth_clients_table.table_name
  description = "DynamoDB table name for OAuth DCR clients"
}

output "auth_codes_table_name" {
  value       = module.auth_codes_table.table_name
  description = "DynamoDB table name for OAuth authorization codes"
}

output "mcp_encryption_key_secret_arn" {
  value       = aws_secretsmanager_secret.mcp_encryption_key.arn
  description = "Secrets Manager secret ARN — populate its value out-of-band after apply (see README.md)"
}
