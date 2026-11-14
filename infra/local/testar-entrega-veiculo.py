"""Confere entrega idempotente, inclusive concorrente, sem alterar execucao, estoque ou pagamento."""

import uuid
from concurrent.futures import ThreadPoolExecutor
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Entrega " + str(uuid.uuid4()), "preco": 100}, "POST")
    peca = chamar(EXECUCAO, "/api/pecas", e,
                 {"nome": "Filtro " + str(uuid.uuid4()), "precoUnitario": 50, "quantidadeEstoque": 10}, "POST")
    ordem = criar_ordem(o)
    rota_entrega = f"/api/ordensservico/{ordem['id']}/entregar"
    chamar(ORDENS, rota_entrega, metodo="POST", status_esperado=401)
    chamar(ORDENS, f"/api/ordensservico/{uuid.uuid4()}/entregar", o, metodo="POST", status_esperado=404)
    chamar(ORDENS, rota_entrega, o, metodo="POST", status_esperado=409)
    saga = aguardar_saga(o, ordem["id"], "RegistroConfirmado")
    id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-diagnostico", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-diagnostico", e,
           {"diagnostico": "Substituir filtros", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": peca["id"], "quantidade": 2}]}, "POST")
    saga = aguardar_saga(o, ordem["id"], "OrcamentoGerado")
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", c, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "ExecucaoLiberada")
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "EmExecucao")
    chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-reparo", e, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "Finalizada")
    finalizada = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)
    estoque = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    execucao = chamar(EXECUCAO, f"/api/execucoes/{id}", e)
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    with ThreadPoolExecutor(max_workers=6) as pool:
        entregas = list(pool.map(lambda _: chamar(ORDENS, rota_entrega, o, metodo="POST"), range(6)))
    data_entrega = None
    for entregue in entregas:
        assert entregue["status"] == "Entregue" and len(entregue["historico"]) == len(finalizada["historico"]) + 1
        registros = [item for item in entregue["historico"] if item["status"] == "Entregue"]
        assert len(registros) == 1
        data_entrega = data_entrega or registros[0]["registradoEm"]
        assert registros[0]["registradoEm"] == data_entrega
    repetida = chamar(ORDENS, rota_entrega, o, metodo="POST")
    consulta = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)
    assert repetida["historico"] == consulta["historico"] == entregas[0]["historico"]
    assert all(item["id"] != ordem["id"] for item in chamar(ORDENS, "/api/ordensservico", o))
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/saga-abertura", o)["status"] == "Finalizada"
    assert chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e) == estoque
    atual = chamar(EXECUCAO, f"/api/execucoes/{id}", e)
    assert atual["status"] == "Finalizada" and atual["historico"] == execucao["historico"]
    assert atual["reservasPecas"] == execucao["reservasPecas"]
    financeiro = chamar(COBRANCA, f"/api/pagamentos/{pagamento['id']}", c)
    assert financeiro["status"] == pagamento["status"] and financeiro["tentativas"] == pagamento["tentativas"]
    print(f"OK: OS {ordem['id']} entregue, historico preservado e um unico registro de entrega")
    print("OK: seis chamadas simultaneas e uma repeticao sem duplicidade; Saga, Execucao, estoque e pagamento preservados")


if __name__ == "__main__":
    main()
