"""Configuracao exclusiva do deploy de Ordens."""

import secrets

IMAGE_BUILDS = {"api_image": ("oficina-mecanica-ordens", "Dockerfile")}


def new_private_secrets():
    return {"database_password": secrets.token_hex(24), "database_root_password": secrets.token_hex(24)}


def service_values(common, private):
    return {
        "database_secrets": {"MYSQL_DATABASE": "ordens", "MYSQL_USER": "ordens_app",
                             "MYSQL_PASSWORD": private["database_password"],
                             "MYSQL_ROOT_PASSWORD": private["database_root_password"]},
        "api_secrets": {
            "JwtSettings__SecretKey": common["jwt_secret"], "AdminUserSettings__Username": "admin",
            "AdminUserSettings__Password": common["admin_password"],
            "OrcamentoCliente__SecretKey": common["ordens_cobranca_secret"],
            "ConnectionStrings__DefaultConnection": "Server=mysql;Database=ordens;User=ordens_app;Password="
                + private["database_password"] + ";SslMode=None",
        },
    }
