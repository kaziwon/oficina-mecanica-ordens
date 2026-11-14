"""Apoio aos testes locais pelos endpoints publicos dos microsservicos."""

import json
import os
import secrets
import time
import uuid
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

ORDENS = os.getenv("ORDENS_URL", "http://localhost:18091").rstrip("/")
EXECUCAO = os.getenv("EXECUCAO_URL", "http://localhost:18092").rstrip("/")
COBRANCA = os.getenv("COBRANCA_URL", "http://localhost:18093").rstrip("/")
PASSWORD = os.getenv("SAGA_ADMIN_PASSWORD", "LocalAdminSaga123")


def chamar(base, rota, token=None, body=None, metodo="GET", status_esperado=None, extra_headers=None):
    headers = {"Content-Type": "application/json", "X-Correlation-ID": "teste-saga-" + str(uuid.uuid4())}
    if token:
        headers["Authorization"] = "Bearer " + token
    if extra_headers:
        headers.update(extra_headers)
    request = Request(base + rota, data=json.dumps(body).encode() if body is not None else None,
                      headers=headers, method=metodo)
    try:
        with urlopen(request, timeout=15) as response:
            if status_esperado is not None:
                assert response.status == status_esperado, f"Esperado {status_esperado}, recebido {response.status}"
            if response.status == 204:
                return None
            return json.load(response)
    except HTTPError as error:
        if error.code == status_esperado:
            return None
        raise RuntimeError(f"{metodo} {rota}: HTTP {error.code}: {error.read().decode()}") from error


def autenticar(base):
    limite = time.monotonic() + 90
    while True:
        try:
            chamar(base, "/health/ready")
            break
        except (URLError, RuntimeError, TimeoutError, ConnectionError):
            if time.monotonic() >= limite:
                raise TimeoutError(f"API nao ficou pronta: {base}")
            time.sleep(1)
    return chamar(base, "/api/auth/login", body={"username": "admin", "password": PASSWORD}, metodo="POST")["token"]


def cpf_teste():
    digitos = [secrets.randbelow(10) for _ in range(9)]
    for tamanho in (9, 10):
        soma = sum(valor * (tamanho + 1 - indice) for indice, valor in enumerate(digitos))
        resto = (soma * 10) % 11
        digitos.append(0 if resto == 10 else resto)
    return "".join(map(str, digitos))


def criar_ordem(token):
    letras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    placa = "".join(secrets.choice(letras) for _ in range(3)) + str(secrets.randbelow(10))
    placa += secrets.choice("ABCDEFGHIJ") + f"{secrets.randbelow(100):02d}"
    return chamar(ORDENS, "/api/ordensservico", token, {
        "cliente": {"nome": "Teste distribuido", "cpfCnpj": cpf_teste(),
                    "email": "teste@example.com", "telefone": "11999999999"},
        "veiculo": {"placa": placa, "marca": "Fiat", "modelo": "Uno", "ano": 2020},
        "descricaoSolicitacao": "Revisar freios - " + str(uuid.uuid4())
    }, "POST")


def aguardar_saga(token, ordem_id, esperado):
    estados = {esperado} if isinstance(esperado, str) else set(esperado)
    limite = time.monotonic() + 90
    ultima = None
    while time.monotonic() < limite:
        ultima = chamar(ORDENS, f"/api/ordensservico/{ordem_id}/saga-abertura", token)
        if ultima["status"] in estados:
            return ultima
        time.sleep(1)
    raise TimeoutError(f"Saga nao chegou a {esperado}: {ultima}")


def preparar_orcamento(ordens_token, execucao_token, servico_id):
    ordem = criar_ordem(ordens_token)
    saga = aguardar_saga(ordens_token, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/iniciar-diagnostico", execucao_token, metodo="POST")
    aguardar_saga(ordens_token, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/concluir-diagnostico", execucao_token,
           {"diagnostico": "Revisao completa", "servicoIds": [servico_id], "pecasInsumos": []}, "POST")
    return ordem, aguardar_saga(ordens_token, ordem["id"], "OrcamentoGerado")
