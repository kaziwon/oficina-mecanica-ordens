"""Testa compensacao paga pelas tres APIs, sem acesso direto aos bancos ou AWS."""

import argparse
import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def preparar_pago(o, e, c, iniciar=True):
    servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Compensacao " + str(uuid.uuid4()), "preco": 100}, "POST")
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
    if iniciar:
        chamar(EXECUCAO, f"/api/execucoes/{id}/iniciar-reparo", e, metodo="POST")
        saga = aguardar_saga(o, ordem["id"], "EmExecucao")
        assert chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)["quantidadeReservada"] == 2
    return ordem, saga, peca


def cancelar(o, e, c, resultado, iniciar):
    ordem, saga, peca = preparar_pago(o, e, c, iniciar)
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    rota = f"/api/ordensservico/{ordem['id']}/cancelar-reparo"
    chamar(ORDENS, rota, o, {"motivo": "Desistencia para teste"}, "POST", 202)
    esperado = "CompensadaAposPagamento" if resultado == "refunded" else "EstornoRequerIntervencao"
    saga = aguardar_saga(o, ordem["id"], esperado)
    execucao = chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}", e)
    assert execucao["status"] == "Cancelada" and execucao["reparoCanceladoEm"]
    if iniciar:
        assert len(execucao["reservasPecas"]) == 1
        assert execucao["reservasPecas"][0]["liberadaEm"] and not execucao["reservasPecas"][0]["consumidaEm"]
    else:
        assert execucao["reservasPecas"] == []
    estoque = chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)
    assert estoque["quantidadeEstoque"] == 10 and estoque["quantidadeReservada"] == 0
    estorno = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}/estorno", c)
    assert estorno["id"] == saga["estornoId"] and estorno["valor"] == 200
    assert estorno["status"] == {"refunded": "Confirmado", "rejected": "Recusado", "unavailable": "FalhaRequerIntervencao"}[resultado]
    atual = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)
    assert atual["status"] == ("Cancelada" if resultado == "refunded" else "CancelamentoPendente")
    assert bool(saga.get("estornadoEm")) == (resultado == "refunded")
    chamar(ORDENS, rota, o, {"motivo": "Desistencia para teste"}, "POST", 202)
    assert chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}/estorno", c) == estorno
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", o)["historico"] == atual["historico"]
    financeiro = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
    assert financeiro["status"] == pagamento["status"] == "Confirmado" and financeiro["tentativas"] == pagamento["tentativas"]
    chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}/concluir-reparo", e, metodo="POST", status_esperado=409)
    chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}/iniciar-reparo", e, metodo="POST", status_esperado=409)
    chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/entregar", o, metodo="POST", status_esperado=409)
    print(f"OK: OS {ordem['id']}: {esperado}; estoque fisico preservado, reservas liberadas e um estorno auditavel")


def recusar_finalizada(o, e, c):
    ordem, saga, peca = preparar_pago(o, e, c)
    chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}/concluir-reparo", e, metodo="POST")
    aguardar_saga(o, ordem["id"], "Finalizada")
    chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/cancelar-reparo", o, {"motivo": "Nao deve cancelar"}, "POST", 409)
    chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}/estorno", c, status_esperado=404)
    assert chamar(EXECUCAO, f"/api/pecas/{peca['id']}", e)["quantidadeEstoque"] == 8
    print("OK: reparo finalizado nao e desfeito e nao produz estorno nem reposicao ficticia de pecas")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resultado", choices=["refunded", "rejected", "unavailable"], default="refunded",
                        help="Resultado configurado previamente no mock; o script nao altera containers.")
    args = parser.parse_args()
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    cancelar(o, e, c, args.resultado, True)
    if args.resultado == "refunded":
        cancelar(o, e, c, args.resultado, False)
        recusar_finalizada(o, e, c)


if __name__ == "__main__":
    main()
