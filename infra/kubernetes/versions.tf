terraform {
  required_version = ">= 1.7.0, < 2.0.0"
  backend "local" {}
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "3.5.0"
    }
  }
}

provider "kubernetes" {
  config_path    = "${pathexpand(var.runtime_dir)}/kubeconfig"
  config_context = "kind-oficina-fase4"
}

variable "runtime_dir" {
  type    = string
  default = "~/.local/share/oficina-fase4"
}

variable "new_relic_enabled" {
  type    = bool
  default = false
}

variable "api_image" {
  type = string
  validation {
    condition     = can(regex("^[a-zA-Z0-9/_.:@-]+$", var.api_image))
    error_message = "Informe uma referencia de imagem valida e imutavel."
  }
}

variable "api_secrets" {
  type      = map(string)
  sensitive = true
}

variable "database_secrets" {
  type      = map(string)
  sensitive = true
}
