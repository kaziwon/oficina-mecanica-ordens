"""Confere reserva transacional, falta de pecas, repeticao e avancos da Saga por HTTP."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Reparo " + str(uuid.uuid4()), "preco": 100}, "POST")
    peca = chamar(EXECUCAO, "/api/pecas", e,
                 {"nome": "Peca " + str(uuid.uuid4()), "precoUnitario": 50, "quantidadeEstoque": 1}, "POST")
    ordem = criar_ordem(o)
    saga = aguardar_saga(o, ordem["id"], "RegistroConfirmado")
    id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST", status_esperado=409)
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-diagnostico", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{id}/concluir-diagnostico", e,
           {"diagnostico": "Trocar dois filtros", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": peca["id"], "quantidade": 2}]}, "POST")
    saga = aguardar_saga(o, ordem["id"], "OrcamentoGerado")
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", c, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "ExecucaoLiberada")
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST", status_esperado=409)
    estoque = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    assert estoque["quantidadeReservada"] == 0 and estoque["quantidadeDisponivel"] == 1
    assert chamar(EXECUCAO, f"/api/execucoes/{id}", e)["status"] == "AguardandoExecucao"
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/saga-abertura", o)["status"] == "ExecucaoLiberada"
    chamar(EXECUCAO, f"/api/pecas/{peca['id']}/movimentacoes", e, {"quantidade": 2}, "POST")
    inicio = chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
    saga = aguardar_saga(o, ordem["id"], "EmExecucao")
    assert inicio["status"] == "EmExecucao" and inicio["reparoIniciadoEm"] and len(inicio["reservasPecas"]) == 1
    assert inicio["reservasPecas"][0]["quantidade"] == 2
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)["status"] == "EmExecucao"
    for _ in range(2):
        repetida = chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
        assert repetida["reparoIniciadoEm"] == inicio["reparoIniciadoEm"]
        assert len(repetida["historico"]) == len(inicio["historico"])
    estoque = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    assert estoque["quantidadeEstoque"] == 3 and estoque["quantidadeReservada"] == 2 and estoque["quantidadeDisponivel"] == 1
    chamar(EXECUCAO, f"/api/pecas/{peca['id']}/movimentacoes", e, {"quantidade": -2}, "POST", status_esperado=409)
    chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e, metodo="DELETE", status_esperado=409)
    atual = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    assert atual["id"] == pagamento["id"] and atual["tentativas"] == pagamento["tentativas"]
    print(f"OK: OS {ordem['id']} e Execucao {id} em execucao apos inicio explicito")
    print("OK: falta de saldo nao iniciou; reposicao permitiu reservar uma vez, sem consumo fisico ou nova cobranca")


if __name__ == "__main__":
    main()
