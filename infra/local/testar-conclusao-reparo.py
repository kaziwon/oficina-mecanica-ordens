"""Confere conclusao, consumo unico das reservas e finalizacao da OS somente por HTTP."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Conclusao " + str(uuid.uuid4()), "preco": 100}, "POST")
    peca = chamar(EXECUCAO, "/api/pecas", e,
                 {"nome": "Filtro " + str(uuid.uuid4()), "precoUnitario": 50, "quantidadeEstoque": 10}, "POST")
    ordem = criar_ordem(o)
    saga = aguardar_saga(o, ordem["id"], "RegistroConfirmado")
    id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-diagnostico", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-diagnostico", e,
           {"diagnostico": "Trocar filtros", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": peca["id"], "quantidade": 2}]}, "POST")
    saga = aguardar_saga(o, ordem["id"], "OrcamentoGerado")
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", c, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "ExecucaoLiberada")
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-reparo", e, metodo="POST", status_esperado=409)
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "EmExecucao")
    reservado = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    assert reservado["quantidadeEstoque"] == 10 and reservado["quantidadeReservada"] == 2
    fim = chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-reparo", e, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "Finalizada")
    assert fim["status"] == "Finalizada" and fim["reparoConcluidoEm"]
    assert fim["reservasPecas"][0]["consumidaEm"] == fim["reparoConcluidoEm"]
    for _ in range(3):
        repetida = chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-reparo", e, metodo="POST")
        assert repetida["reparoConcluidoEm"] == fim["reparoConcluidoEm"] and len(repetida["historico"]) == 6
    tardio = chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
    assert tardio["status"] == "Finalizada"
    estoque = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    assert estoque["quantidadeEstoque"] == 8 and estoque["quantidadeReservada"] == 0
    assert estoque["quantidadeDisponivel"] == reservado["quantidadeDisponivel"] == 8
    finalizada = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)
    assert finalizada["status"] == "Finalizada" and len(finalizada["historico"]) == 8
    assert saga["reparoConcluidoEm"] and saga["pagamentoId"] == pagamento["id"]
    assert all(item["id"] != ordem["id"] for item in chamar(ORDENS, "/api/ordensservico", o))
    assert all(item["id"] != id for item in chamar(EXECUCAO, "/api/execucoes", e))
    atual = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    assert atual["id"] == pagamento["id"] and atual["tentativas"] == pagamento["tentativas"]
    print(f"OK: OS {ordem['id']} e Execucao {id} finalizadas, sem marcar entrega")
    print("OK: duas pecas consumidas uma vez; historico preservado, filas sem finalizadas e pagamento sem nova tentativa")


if __name__ == "__main__":
    main()
