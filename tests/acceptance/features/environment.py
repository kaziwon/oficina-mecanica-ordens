import sys
from pathlib import Path
from urllib.parse import urlparse

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "infra" / "local"))
from saga_http import ORDENS, EXECUCAO, COBRANCA, autenticar


def before_all(context):
    for url in (ORDENS, EXECUCAO, COBRANCA):
        if urlparse(url).hostname not in ("localhost", "127.0.0.1", "::1"):
            raise ValueError("Esta suite cria dados de teste e aceita somente APIs locais.")
    context.o, context.e, context.c = (autenticar(url) for url in (ORDENS, EXECUCAO, COBRANCA))
