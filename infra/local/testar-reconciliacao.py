"""Testa recuperacao financeira pelas APIs locais com respostas perdidas no mock."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, preparar_orcamento, aguardar_saga


def main():
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Reconciliacao " + str(uuid.uuid4()), "preco": 100}, "POST")
    ordem, saga = preparar_orcamento(o, e, servico["id"])
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", c, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "FalhaRequerIntervencao")
    id = saga["pagamentoId"]; rota = f"/api/pagamentos/{id}/reconciliar"
    incerto = chamar(COBRANCA, f"/api/pagamentos/{id}", c)
    assert incerto["status"] == "FalhaRequerIntervencao"
    chamar(COBRANCA, rota, c, metodo="POST", status_esperado=204)
    recuperado = chamar(COBRANCA, f"/api/pagamentos/{id}", c)
    assert recuperado["id"] == id and recuperado["status"] == "Confirmado"
    assert recuperado["tentativas"] == incerto["tentativas"] and recuperado["consultasReconciliacao"] == 1
    chamar(COBRANCA, rota, c, metodo="POST", status_esperado=204)
    assert chamar(COBRANCA, f"/api/pagamentos/{id}", c) == recuperado
    saga = aguardar_saga(o, ordem["id"], "ExecucaoLiberada")
    print("OK: pagamento original reconciliado e execucao liberada, sem nova cobranca")
    chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/cancelar-reparo", o, {"motivo": "Teste de recuperacao"}, "POST", 202)
    saga = aguardar_saga(o, ordem["id"], "EstornoRequerIntervencao")
    rota_estorno = f"/api/pagamentos/{id}/estorno"
    incerto = chamar(COBRANCA, rota_estorno, c)
    assert incerto["status"] == "FalhaRequerIntervencao"
    chamar(COBRANCA, rota_estorno + "/reconciliar", c, metodo="POST", status_esperado=204)
    estorno = chamar(COBRANCA, rota_estorno, c)
    assert estorno["id"] == incerto["id"] == saga["estornoId"] and estorno["status"] == "Confirmado"
    assert estorno["tentativas"] == incerto["tentativas"] and estorno["consultasReconciliacao"] == 1
    chamar(COBRANCA, rota_estorno + "/reconciliar", c, metodo="POST", status_esperado=204)
    assert chamar(COBRANCA, rota_estorno, c) == estorno
    aguardar_saga(o, ordem["id"], "CompensadaAposPagamento")
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)["status"] == "Cancelada"
    assert chamar(COBRANCA, f"/api/pagamentos/{id}", c) == recuperado
    print("OK: estorno original reconciliado; Saga cancelou a OS e preservou a cobranca para auditoria")


if __name__ == "__main__":
    main()
