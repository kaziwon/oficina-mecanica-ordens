"""Confere login por CPF e isolamento entre clientes nas APIs locais da Fase 4."""

import base64
import json
import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, cpf_teste


def main():
    admin = autenticar(ORDENS)
    a, b = criar_ordem(admin), criar_ordem(admin)
    ca = chamar(ORDENS, f"/api/clientes/{a['clienteId']}", admin)
    cb = chamar(ORDENS, f"/api/clientes/{b['clienteId']}", admin)
    login_a = chamar(ORDENS, "/api/auth/cliente", body={"cpf": ca["cpfCnpj"]}, metodo="POST")
    cpf_b = cb["cpfCnpj"]
    formatado_b = f"{cpf_b[:3]}.{cpf_b[3:6]}.{cpf_b[6:9]}-{cpf_b[9:]}"
    login_b = chamar(ORDENS, "/api/auth/cliente", body={"cpf": formatado_b}, metodo="POST")
    ta, tb = login_a["token"], login_b["token"]
    assert login_a["clienteId"] == a["clienteId"] and login_b["clienteId"] == b["clienteId"]
    chamar(ORDENS, "/api/minhas-ordens", status_esperado=401)
    chamar(ORDENS, "/api/minhas-ordens", admin, status_esperado=401)
    for token, propria, alheia in [(ta, a, b), (tb, b, a)]:
        lista = chamar(ORDENS, f"/api/minhas-ordens?clienteId={alheia['clienteId']}", token)
        assert [item["id"] for item in lista] == [propria["id"]]
        assert chamar(ORDENS, f"/api/minhas-ordens/{propria['id']}", token)["id"] == propria["id"]
        chamar(ORDENS, f"/api/minhas-ordens/{alheia['id']}", token, status_esperado=404)
        chamar(ORDENS, f"/api/minhas-ordens/{uuid.uuid4()}", token, status_esperado=404)
        for rota in ["/api/clientes", "/api/veiculos", "/api/ordensservico"]:
            chamar(ORDENS, rota, token, status_esperado=401)
        chamar(ORDENS, f"/api/ordensservico/{propria['id']}/entregar", token, metodo="POST", status_esperado=401)
        chamar(ORDENS, f"/api/ordensservico/{propria['id']}/cancelar-reparo", token,
               {"motivo": "Nao permitido ao cliente neste endpoint"}, "POST", 401)
        chamar(EXECUCAO, "/api/execucoes", token, status_esperado=401)
        chamar(COBRANCA, "/api/orcamentos", token, status_esperado=401)
    segmentos = ta.split(".")
    payload = json.loads(base64.urlsafe_b64decode(segmentos[1] + "=" * (-len(segmentos[1]) % 4)))
    assert payload["sub"] == a["clienteId"] and payload["role"] == "Cliente" and "cpf" not in payload
    payload["sub"], payload["role"] = b["clienteId"], "Admin"
    segmentos[1] = base64.urlsafe_b64encode(json.dumps(payload).encode()).decode().rstrip("=")
    chamar(ORDENS, "/api/minhas-ordens", ".".join(segmentos), status_esperado=401)
    chamar(ORDENS, "/api/auth/cliente", body={"cpf": "00000000000"}, metodo="POST", status_esperado=400)
    chamar(ORDENS, "/api/auth/cliente", body={"cpf": cpf_teste()}, metodo="POST", status_esperado=401)
    chamar(ORDENS, "/api/auth/cliente", body={"cpf": ca["cpfCnpj"], "role": "Admin"}, metodo="POST", status_esperado=400)
    # Desativa somente o cliente recem-criado pelo teste; nao altera cadastros anteriores.
    chamar(ORDENS, f"/api/clientes/{a['clienteId']}", admin, metodo="DELETE", status_esperado=204)
    chamar(ORDENS, "/api/minhas-ordens", ta, status_esperado=403)
    chamar(ORDENS, f"/api/minhas-ordens/{a['id']}", ta, status_esperado=403)
    chamar(ORDENS, "/api/auth/cliente", body={"cpf": ca["cpfCnpj"]}, metodo="POST", status_esperado=401)
    assert chamar(ORDENS, f"/api/ordensservico/{a['id']}", admin)["id"] == a["id"]
    assert chamar(ORDENS, f"/api/minhas-ordens/{b['id']}", tb)["id"] == b["id"]
    print("OK: CPF simples e formatado; cada cliente ve apenas suas OS, sem escolher outra identidade pela query")
    print("OK: token adulterado, rotas administrativas e outros servicos bloqueados; desativacao revoga consultas")
    print("OK: historico preservado; nenhum token ou CPF foi exibido pelo roteiro")


if __name__ == "__main__":
    main()
