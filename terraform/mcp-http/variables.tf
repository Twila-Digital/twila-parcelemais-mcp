variable "region" {
  type        = string
  description = "AWS region"
}

variable "shared_ecs_cluster_name" {
  type        = string
  description = "Name of the existing shared ECS cluster to deploy onto (looked up via data source — no raw ARN/ID committed here)"
}

variable "shared_alb_name" {
  type        = string
  description = "Name of the existing shared ALB (looked up via data source — vpc_id, arn_suffix and the ALB's security group are all derived from this, no raw ID committed here)"
}

variable "health_check_path" {
  type        = string
  default     = "/health"
  description = "The destination for the health check request"
}

variable "health_check_matcher" {
  type        = string
  default     = "200"
  description = "The HTTP response codes to indicate a healthy check"
}

variable "task_cpu" {
  type        = number
  description = "The number of CPU units used by the task. If unspecified, defaults to `container_cpu`"
  default     = null
}

variable "task_memory" {
  type        = number
  description = "The amount of memory (in MiB) used by the task. If unspecified, defaults to `container_memory`"
  default     = null
}

variable "container_cpu" {
  type        = number
  description = "The vCPU setting to control cpu limits of container (Fargate vCPU table: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html)"
  default     = 256
}

variable "container_memory" {
  type        = number
  description = "The amount of RAM to allow the container to use in MB"
  default     = 512
}

variable "container_memory_reservation" {
  type        = number
  description = "The amount of RAM (soft limit) to allow the container to use in MB"
  default     = 256
}

variable "container_port" {
  type        = number
  description = "The port the app listens on (must match `ASPNETCORE_URLS`/`EXPOSE` in the Dockerfile)"
  default     = 8080
}

variable "desired_count" {
  type        = number
  description = "The desired number of tasks to start with"
  default     = 1
}

variable "ecs_private_subnet_ids" {
  type        = list(string)
  description = <<-EOT
    Private subnet IDs to provision the ECS service onto, in the shared VPC (same VPC as
    `shared_alb_name`). Kept as an explicit variable rather than a `data "aws_subnets"` lookup
    because the exact tag/attribute convention used to mark "private" subnets in the shared VPC
    needs to be confirmed against the real account before it's safe to filter on automatically —
    supply this value out-of-band (see README.md), do not hardcode real subnet IDs in this
    public repository.
  EOT
}

variable "alb_ingress_protocol_version" {
  type        = string
  default     = "HTTP1"
  description = "One of `HTTP1`, `HTTP2`, `GRPC`"
}

variable "alb_ingress_unauthenticated_listener_arns" {
  type        = list(string)
  description = "Listener ARN(s) of the shared ALB where the MCP host rule should be attached"
  default     = []
}

variable "alb_ingress_unauthenticated_hosts" {
  type        = list(string)
  description = <<-EOT
    Host header(s) routed to this service (e.g. `mcp.parcelemais.com.br`). Host-based routing is
    used instead of path-based because the app serves several top-level paths (`/mcp`, `/authorize`,
    `/token`, `/register`, `/revoke`, `/.well-known/*`, `/health`) and the `alb-ingress` module only
    supports a single path pattern per rule — a dedicated host avoids that constraint entirely and
    keeps this service's routing independent of whatever else shares the ALB.
  EOT
  default     = []
}

variable "alb_ingress_listener_unauthenticated_priority" {
  type        = number
  description = "Priority for the ALB listener rule (1-50000, lower = higher priority). Must not collide with any existing rule's priority on the same listener"
  default     = 4000
}

variable "autoscaling_min_capacity" {
  type        = number
  description = "Minimum number of running tasks"
  default     = 1
}

variable "autoscaling_max_capacity" {
  type        = number
  description = "Maximum number of running tasks"
  default     = 2
}

variable "ecs_alarms_enabled" {
  type        = bool
  description = "Whether to enable CloudWatch alarms for ECS service metrics"
  default     = false
}

variable "cloudwatch_log_group_enabled" {
  type        = bool
  description = "Whether to create a CloudWatch log group for the container"
  default     = true
}

variable "dynamodb_table_prefix" {
  type        = string
  description = "Prefix for the DynamoDB table names, must match `MCP_DYNAMODB_TABLE_PREFIX` passed to the container"
  default     = "ParcelemaisMcp"
}
