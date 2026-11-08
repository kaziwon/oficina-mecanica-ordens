"""Verifica o primeiro fluxo distribuido usando somente as APIs publicas."""

import json
import os
import secrets
import time
import uuid
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


ORDENS = os.getenv("ORDENS_URL", "http://localhost:18091").rstrip("/")
EXECUCAO = os.getenv("EXECUCAO_URL", "http://localhost:18092").rstrip("/")
PASSWORD = os.getenv("SAGA_ADMIN_PASSWORD", "LocalAdminSaga123")


def chamar(base, rota, token=None, body=None, metodo="GET"):
    headers = {"Content-Type": "application/json", "X-Correlation-ID": "teste-saga-" + str(uuid.uuid4())}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = json.dumps(body).encode() if body is not None else None
    request = Request(base + rota, data=data, headers=headers, method=metodo)
    try:
        with urlopen(request, timeout=15) as response:
            return json.load(response)
    except HTTPError as error:
        raise RuntimeError(f"{metodo} {rota}: HTTP {error.code}: {error.read().decode()}") from error


def cpf_teste():
    digitos = [secrets.randbelow(10) for _ in range(9)]
    for tamanho in (9, 10):
        soma = sum(valor * (tamanho + 1 - indice) for indice, valor in enumerate(digitos))
        resto = (soma * 10) % 11
        digitos.append(0 if resto == 10 else resto)
    return "".join(map(str, digitos))


def criar_ordem(token):
    placa = f"ABC{1000 + secrets.randbelow(9000)}"
    return chamar(ORDENS, "/api/ordensservico", token, {
        "cliente": {"nome": "Teste distribuido", "cpfCnpj": cpf_teste(),
                    "email": "teste@example.com", "telefone": "11999999999"},
        "veiculo": {"placa": placa, "marca": "Fiat", "modelo": "Uno", "ano": 2020},
        "descricaoSolicitacao": "Revisar freios - " + str(uuid.uuid4())
    }, "POST")


def aguardar_saga(token, ordem_id, esperado):
    limite = time.monotonic() + 90
    ultima = None
    while time.monotonic() < limite:
        ultima = chamar(ORDENS, f"/api/ordensservico/{ordem_id}/saga-abertura", token)
        if ultima["status"] == esperado:
            return ultima
        time.sleep(1)
    raise TimeoutError(f"Saga nao chegou a {esperado}: {ultima}")


def main():
    for base in (ORDENS, EXECUCAO):
        limite = time.monotonic() + 90
        while True:
            try:
                chamar(base, "/health/ready")
                break
            except (URLError, RuntimeError, TimeoutError):
                if time.monotonic() >= limite:
                    raise TimeoutError(f"API nao ficou pronta: {base}")
                time.sleep(1)
    token_ordens = chamar(ORDENS, "/api/auth/login", body={"username": "admin", "password": PASSWORD}, metodo="POST")["token"]
    token_execucao = chamar(EXECUCAO, "/api/auth/login", body={"username": "admin", "password": PASSWORD}, metodo="POST")["token"]

    ordem = criar_ordem(token_ordens)
    saga = aguardar_saga(token_ordens, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    execucao = chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", token_execucao)
    assert execucao["ordemServicoId"] == ordem["id"]
    assert execucao["status"] == "AguardandoDiagnostico"
    print(f"OK: OS {ordem['id']} registrada em Execucao {execucao_id}")

    rota = f"/api/ordensservico/{ordem['id']}/cancelar-entrada"
    chamar(ORDENS, rota, token_ordens, {"motivo": "Desistencia antes do diagnostico"}, "POST")
    aguardar_saga(token_ordens, ordem["id"], "Compensada")
    cancelada = chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", token_execucao)
    assert cancelada["status"] == "Cancelada"
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", token_ordens)["status"] == "Cancelada"
    chamar(ORDENS, rota, token_ordens, {"motivo": "Repeticao do pedido"}, "POST")
    assert len(chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", token_execucao)["historico"]) == len(cancelada["historico"])
    print("OK: compensacao confirmada nos dois servicos; repeticao sem novo historico")

    ordem = criar_ordem(token_ordens)
    saga = aguardar_saga(token_ordens, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/iniciar-diagnostico", token_execucao, metodo="POST")
    chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/cancelar-entrada", token_ordens,
           {"motivo": "Cancelamento depois do inicio"}, "POST")
    aguardar_saga(token_ordens, ordem["id"], "FalhaRequerIntervencao")
    assert chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", token_execucao)["status"] == "EmDiagnostico"
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", token_ordens)["status"] != "Cancelada"
    print("OK: diagnostico iniciado nao foi cancelado automaticamente")


if __name__ == "__main__":
    main()
