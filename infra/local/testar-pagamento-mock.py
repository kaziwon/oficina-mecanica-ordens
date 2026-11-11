"""Verifica token, pagamento HTTP, resultado na Saga e idempotencia, sem dinheiro real."""

import argparse
import os
import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, aguardar_saga, preparar_orcamento


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--esperado", choices=("approved", "rejected", "unavailable"), default="approved")
    modo = parser.parse_args().esperado
    ordens_token = autenticar(ORDENS)
    execucao_token = autenticar(EXECUCAO)
    cobranca_token = autenticar(COBRANCA)
    servico = chamar(EXECUCAO, "/api/servicos", execucao_token,
                     {"nome": "Pagamento " + str(uuid.uuid4()), "preco": 100}, "POST")
    ordem, inicial = preparar_orcamento(ordens_token, execucao_token, servico["id"])
    for _ in range(2):
        chamar(COBRANCA, f"/api/orcamentos/{inicial['orcamentoId']}/aprovar", cobranca_token, metodo="POST")
    estado = "PagamentoConfirmado" if modo == "approved" else "FalhaRequerIntervencao"
    saga = aguardar_saga(ordens_token, ordem["id"], estado)
    orcamento = chamar(COBRANCA, f"/api/orcamentos/{inicial['orcamentoId']}", cobranca_token)
    assert saga["pagamentoId"] == orcamento["pagamentoId"] and saga["pagamentoId"]
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", cobranca_token)
    esperado = {"approved": "Confirmado", "rejected": "Recusado", "unavailable": "FalhaRequerIntervencao"}[modo]
    assert pagamento["status"] == esperado and pagamento["valor"] == 100 and pagamento["moeda"] == "BRL"
    assert pagamento["tentativas"] >= 1
    status_os = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"]
    assert status_os == ("PagamentoConfirmado" if modo == "approved" else "AguardandoPagamento")
    assert chamar(EXECUCAO, f"/api/execucoes/{inicial['execucaoId']}", execucao_token)["status"] == "AguardandoLiberacao"
    mock_url = os.getenv("PAGAMENTO_MOCK_URL", "http://localhost:18094").rstrip("/")
    token = chamar(mock_url, "/oauth/token", body={"grant_type": "client_credentials",
                   "client_id": os.getenv("PAGAMENTO_MOCK_CLIENT_ID", "oficina-local"),
                   "client_secret": os.getenv("PAGAMENTO_MOCK_CLIENT_SECRET", "segredo-do-mock-somente-local")}, metodo="POST")["access_token"]
    if modo == "unavailable":
        chamar(mock_url, f"/payments/{saga['pagamentoId']}", token, status_esperado=404)
        assert pagamento["provedorId"] is None and saga["pagamentoConfirmadoEm"] is None
        print("OK: indisponibilidade exigiu reconciliacao, sem inventar recusa ou confirmacao")
    else:
        registro = chamar(mock_url, f"/payments/{saga['pagamentoId']}", token)
        repetida = chamar(mock_url, "/payments", token,
                          {"external_reference": saga["pagamentoId"], "transaction_amount": 100, "currency_id": "BRL"}, "POST",
                          extra_headers={"X-Idempotency-Key": saga["pagamentoId"], "X-Correlation-ID": saga["correlationId"]})
        assert registro["id"] == repetida["id"] == pagamento["provedorId"] == saga["pagamentoProvedorId"]
        assert registro["status"] == repetida["status"] == modo
        print(f"OK: token obtido e transacao {registro['id']} repetida sem segunda cobranca")
    print(f"OK: OS {ordem['id']}, pagamento {pagamento['id']} em {pagamento['status']}; reparo segue bloqueado")


if __name__ == "__main__":
    main()
