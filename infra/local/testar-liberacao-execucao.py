"""Confere liberacao apos pagamento, snapshots, estoque e repeticao usando apenas HTTP."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    ordens_token = autenticar(ORDENS)
    execucao_token = autenticar(EXECUCAO)
    cobranca_token = autenticar(COBRANCA)
    nome = str(uuid.uuid4())
    servico = chamar(EXECUCAO, "/api/servicos", execucao_token, {"nome": "Liberacao " + nome, "preco": 100}, "POST")
    peca = chamar(EXECUCAO, "/api/pecas", execucao_token,
                 {"nome": "Filtro " + nome, "precoUnitario": 50, "quantidadeEstoque": 10}, "POST")
    ordem = criar_ordem(ordens_token)
    saga = aguardar_saga(ordens_token, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/iniciar-diagnostico", execucao_token, metodo="POST")
    aguardar_saga(ordens_token, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/concluir-diagnostico", execucao_token,
           {"diagnostico": "Revisao completa", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": peca["id"], "quantidade": 2}]}, "POST")
    saga = aguardar_saga(ordens_token, ordem["id"], "OrcamentoGerado")
    # Preco atual do catalogo nao substitui a copia do diagnostico pago.
    chamar(EXECUCAO, f"/api/pecas/{peca['id']}", execucao_token,
           {"nome": peca["nome"], "precoUnitario": 80}, "PUT")
    chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", cobranca_token, metodo="POST")
    saga = aguardar_saga(ordens_token, ordem["id"], "ExecucaoLiberada")
    execucao = chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", execucao_token)
    assert execucao["status"] == "AguardandoExecucao" and execucao["liberadaEm"]
    assert execucao["pagamentoId"] == saga["pagamentoId"] and execucao["orcamentoId"] == saga["orcamentoId"]
    assert execucao["pecasInsumos"][0]["precoUnitario"] == 50 and execucao["diagnostico"] == "Revisao completa"
    assert len(execucao["historico"]) == 4
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] == "AguardandoExecucao"
    assert saga["execucaoLiberadaEm"] and saga["pagamentoConfirmadoEm"]
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", cobranca_token)
    assert pagamento["status"] == "Confirmado" and pagamento["valor"] == 200
    assert chamar(EXECUCAO, f"/api/pecas/{peca['id']}", execucao_token)["quantidadeEstoque"] == 10
    fila = chamar(EXECUCAO, "/api/execucoes?status=AguardandoExecucao", execucao_token)
    assert any(item["id"] == execucao_id for item in fila)
    for _ in range(2):
        chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}/aprovar", cobranca_token, metodo="POST")
    repetida = chamar(ORDENS, f"/api/ordensservico/{ordem['id']}/saga-abertura", ordens_token)
    assert repetida["pagamentoId"] == saga["pagamentoId"] and repetida["status"] == "ExecucaoLiberada"
    assert len(chamar(EXECUCAO, f"/api/execucoes/{execucao_id}", execucao_token)["historico"]) == 4
    assert chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", cobranca_token)["tentativas"] == pagamento["tentativas"]
    print(f"OK: OS {ordem['id']} e Execucao {execucao_id} aguardando execucao, pagamento {pagamento['id']} confirmado")
    print("OK: autorizacao vinculada, preco do diagnostico preservado e estoque intacto; reparo nao iniciado")


if __name__ == "__main__":
    main()
