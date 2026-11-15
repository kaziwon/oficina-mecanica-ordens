"""Leitura da configuracao privada de telemetria; nao imprime credenciais."""

import re

from local_runtime import private_json


def read_new_relic(home):
    path = home / "secrets" / "newrelic.json"
    if not path.exists() and not path.is_symlink():
        return {"enabled": False, "license_key": ""}
    settings = private_json(path)
    if not isinstance(settings, dict) or not isinstance(settings.get("enabled"), bool):
        raise ValueError("Configuracao New Relic invalida; execute configure_newrelic.py no infra-local.")
    key = settings.get("license_key", "")
    if not isinstance(key, str) or (settings["enabled"] and not re.fullmatch(r"[A-Za-z0-9_-]{20,256}", key)):
        raise ValueError("License key invalida; configure-a novamente sem copiar o nome da variavel.")
    return {"enabled": settings["enabled"], "license_key": key if settings["enabled"] else ""}
