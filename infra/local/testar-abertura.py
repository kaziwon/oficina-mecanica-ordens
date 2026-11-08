"""Verifica abertura, compensacao e limite do cancelamento usando somente HTTP."""

from saga_http import ORDENS, EXECUCAO, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    token_ordens = autenticar(ORDENS)
    token_execucao = autenticar(EXECUCAO)
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
    aguardar_saga(token_ordens, ordem["id"], "EmDiagnostico")
    chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/cancelar-entrada", token_ordens,
           {"motivo": "Cancelamento depois do inicio"}, "POST", status_esperado=409)
    assert chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", token_execucao)["status"] == "EmDiagnostico"
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", token_ordens)["status"] != "Cancelada"
    print("OK: diagnostico iniciado nao foi cancelado automaticamente")


if __name__ == "__main__":
    main()
