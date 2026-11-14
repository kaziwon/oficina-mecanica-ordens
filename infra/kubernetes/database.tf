locals {
  service_settings = {
    OrcamentoCliente__Enabled = "true"
    OrcamentoCliente__BaseUrl = "http://api.oficina-cobranca.svc.cluster.local:8080/"
  }
  migration_template = merge(local.api_template, {
    job_name = "migrate-${substr(sha256(jsonencode([var.api_image, local.config_checksum, local.secret_checksum])), 0, 12)}"
  })
}

resource "kubernetes_persistent_volume_claim_v1" "database" {
  metadata {
    name      = "database-data"
    namespace = kubernetes_namespace_v1.service.metadata[0].name
  }
  spec {
    access_modes       = ["ReadWriteOnce"]
    storage_class_name = "standard"
    resources { requests = { storage = "1Gi" } }
  }
  wait_until_bound = false
  lifecycle { prevent_destroy = true }
}

resource "kubernetes_secret_v1" "database" {
  metadata {
    name      = "database-secrets"
    namespace = kubernetes_namespace_v1.service.metadata[0].name
  }
  data = var.database_secrets
}

resource "kubernetes_manifest" "database_service" {
  manifest   = yamldecode(templatefile("${path.module}/../../k8s/mysql-service.yaml.tftpl", local.api_template))
  depends_on = [kubernetes_namespace_v1.service]
}

resource "kubernetes_manifest" "database" {
  manifest = yamldecode(templatefile("${path.module}/../../k8s/mysql-statefulset.yaml.tftpl", local.api_template))
  depends_on = [kubernetes_persistent_volume_claim_v1.database, kubernetes_secret_v1.database,
  kubernetes_manifest.database_service]
  wait { rollout = true }
  timeouts {
    create = "10m"
    update = "10m"
  }
}

resource "kubernetes_manifest" "database_init" {
  computed_fields = ["metadata.annotations", "metadata.labels", "spec.template.metadata.labels"]
  manifest        = yamldecode(templatefile("${path.module}/../../k8s/migrations-job.yaml.tftpl", local.migration_template))
  depends_on      = [kubernetes_manifest.database, kubernetes_config_map_v1.api, kubernetes_secret_v1.api]
  wait {
    condition {
      type   = "Complete"
      status = "True"
    }
  }
  timeouts {
    create = "8m"
    update = "8m"
  }
}
