locals {
  deployment_config = jsondecode(file("${path.module}/../../k8s/deployment.json"))
  namespace         = "oficina-${local.deployment_config.service}"
  api_settings = merge({
    ASPNETCORE_ENVIRONMENT                                        = "Production"
    Swagger__Enabled                                              = "true"
    Database__ApplyMigrations                                     = "false"
    Messaging__Enabled                                            = "true"
    Messaging__UsarAws                                            = "false"
    Messaging__Endpoint                                           = "http://elasticmq.oficina-plataforma.svc.cluster.local:9324"
    DOTNET_EnableDiagnostics                                      = var.new_relic_enabled ? "1" : "0"
    CORECLR_ENABLE_PROFILING                                      = var.new_relic_enabled ? "1" : "0"
    NEW_RELIC_AGENT_ENABLED                                       = var.new_relic_enabled ? "true" : "false"
    NEW_RELIC_APP_NAME                                            = "oficina-fase4-${local.deployment_config.service}"
    NEW_RELIC_LOG_LEVEL                                           = "info"
    NEW_RELIC_LOG_CONSOLE                                         = "true"
    NEW_RELIC_LABELS                                              = "environment:local;phase:4;service:${local.deployment_config.service}"
    NEW_RELIC_APPLICATION_LOGGING_ENABLED                         = "true"
    NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED              = "true"
    NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED = "true"
    NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED        = "false"
    Logging__LogLevel__Default                                    = "Information"
    Logging__LogLevel__Microsoft                                  = "Warning"
  }, local.service_settings)
  config_checksum = sha256(jsonencode(local.api_settings))
  secret_checksum = nonsensitive(sha256(jsonencode(var.api_secrets)))
  api_template = {
    namespace       = local.namespace
    api_image       = var.api_image
    node_port       = local.deployment_config.node_port
    config_checksum = local.config_checksum
    secret_checksum = local.secret_checksum
    rollout_checksum = sha256(jsonencode([var.api_image, local.config_checksum, local.secret_checksum,
    file("${path.module}/../../k8s/api-deployment.yaml.tftpl")]))
  }
}

resource "kubernetes_namespace_v1" "service" {
  metadata {
    name = local.namespace
    labels = {
      "app.kubernetes.io/part-of" = "oficina-fase4"
      "app.kubernetes.io/name"    = local.deployment_config.service
    }
  }
  lifecycle { prevent_destroy = true }
}

resource "kubernetes_secret_v1" "api" {
  metadata {
    name      = "api-secrets"
    namespace = kubernetes_namespace_v1.service.metadata[0].name
  }
  data = var.api_secrets
}

resource "kubernetes_config_map_v1" "api" {
  metadata {
    name      = "api-config"
    namespace = kubernetes_namespace_v1.service.metadata[0].name
  }
  data = local.api_settings
}

resource "kubernetes_manifest" "api_service" {
  manifest   = yamldecode(templatefile("${path.module}/../../k8s/api-service.yaml.tftpl", local.api_template))
  depends_on = [kubernetes_namespace_v1.service]
}

resource "kubernetes_manifest" "api" {
  manifest = yamldecode(templatefile("${path.module}/../../k8s/api-deployment.yaml.tftpl", local.api_template))
  depends_on = [kubernetes_manifest.database_init, kubernetes_secret_v1.api,
  kubernetes_config_map_v1.api, kubernetes_manifest.api_service]
  wait { rollout = true }
  timeouts {
    create = "8m"
    update = "8m"
  }
}

output "api_url" {
  value = "http://127.0.0.1:${local.deployment_config.host_port}"
}

output "namespace" {
  value = local.namespace
}
