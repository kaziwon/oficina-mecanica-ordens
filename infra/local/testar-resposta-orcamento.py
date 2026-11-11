"""Verifica aprovacao e recusa passando pelos tres servicos, usando somente HTTP."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga
from saga_http import preparar_orcamento as preparar


def main():
    ordens_token = autenticar(ORDENS)
    execucao_token = autenticar(EXECUCAO)
    cobranca_token = autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", execucao_token,
                     {"nome": "Resposta " + str(uuid.uuid4()), "preco": 100}, "POST")

    ordem, saga = preparar(ordens_token, execucao_token, servico["id"])
    for _ in range(2):
        chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", cobranca_token, metodo="POST")
    atual = aguardar_saga(ordens_token, ordem["id"], ("AguardandoPagamento", "PagamentoConfirmado", "LiberacaoExecucaoPendente", "ExecucaoLiberada"))
    assert atual["orcamentoAprovado"] is True
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] in ("AguardandoPagamento", "PagamentoConfirmado", "AguardandoExecucao")
    assert chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}", execucao_token)["status"] in ("AguardandoLiberacao", "AguardandoExecucao")
    orcamento = chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}", cobranca_token)
    assert orcamento["status"] == "Aprovado" and len(orcamento["historico"]) == 2
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/recusar", cobranca_token,
           {"motivo": "Nao pode trocar a resposta"}, "POST", status_esperado=409)
    print(f"OK: OS {ordem['id']} aprovada; reparo nao foi iniciado automaticamente")

    ordem, saga = preparar(ordens_token, execucao_token, servico["id"])
    for _ in range(2):
        chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/recusar", cobranca_token,
               {"motivo": "Cliente nao autorizou o valor"}, "POST")
    atual = aguardar_saga(ordens_token, ordem["id"], "OrcamentoRecusado")
    assert atual["orcamentoAprovado"] is False
    assert atual["motivo"] == "Cliente nao autorizou o valor"
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] == "Cancelada"
    execucao = chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}", execucao_token)
    assert execucao["status"] == "Cancelada" and execucao["diagnostico"] == "Revisao completa"
    assert len(execucao["historico"]) == 4
    assert all(item["id"] != saga["execucaoId"] for item in chamar(EXECUCAO, "/api/execucoes", execucao_token))
    assert all(item["id"] != ordem["id"] for item in chamar(ORDENS, "/api/ordensservico", ordens_token))
    orcamento = chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}", cobranca_token)
    assert orcamento["status"] == "Recusado" and len(orcamento["historico"]) == 2
    assert orcamento["pagamentoId"] is None
    print(f"OK: OS {ordem['id']} recusada, cancelada nos dois servicos, sem apagar diagnostico ou historico")


if __name__ == "__main__":
    main()
