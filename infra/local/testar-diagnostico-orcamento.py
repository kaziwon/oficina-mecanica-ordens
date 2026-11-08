"""Exercita Ordens -> Execucao -> Ordens -> Cobranca -> Ordens, sem consultar bancos."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga


def main():
    ordens_token = autenticar(ORDENS)
    execucao_token = autenticar(EXECUCAO)
    cobranca_token = autenticar(COBRANCA)
    nome = str(uuid.uuid4())
    servico = chamar(EXECUCAO, "/api/servicos", execucao_token,
                     {"nome": "Revisao " + nome, "descricao": "Freios", "preco": 100}, "POST")
    peca = chamar(EXECUCAO, "/api/pecas", execucao_token,
                 {"nome": "Pastilha " + nome, "precoUnitario": 50, "quantidadeEstoque": 10}, "POST")
    ordem = criar_ordem(ordens_token)
    saga = aguardar_saga(ordens_token, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/iniciar-diagnostico", execucao_token, metodo="POST")
    aguardar_saga(ordens_token, ordem["id"], "EmDiagnostico")
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] == "EmDiagnostico"
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/concluir-diagnostico", execucao_token,
           {"diagnostico": "Substituir pastilhas", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": peca["id"], "quantidade": 2}]}, "POST")
    saga = aguardar_saga(ordens_token, ordem["id"], "OrcamentoGerado")
    orcamento = chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}", cobranca_token)
    assert orcamento["ordemServicoId"] == ordem["id"]
    assert orcamento["execucaoId"] == execucao_id
    assert orcamento["valorTotal"] == 200 and saga["valorOrcamento"] == 200
    assert orcamento["status"] == "AguardandoAprovacao" and len(orcamento["itens"]) == 2
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] == "AguardandoAprovacao"
    assert len(chamar(COBRANCA, f"/api/orcamentos?ordemServicoId={ordem['id']}", cobranca_token)) == 1
    print(f"OK: OS {ordem['id']}, Execucao {execucao_id}, Orcamento {saga['orcamentoId']} total R$ 200,00")
    print("OK: OS aguardando aprovacao; diagnostico e precos copiados sem consultar outro banco")

    ordem = criar_ordem(ordens_token)
    saga = aguardar_saga(ordens_token, ordem["id"], "RegistroConfirmado")
    execucao_id = saga["execucaoId"]
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/iniciar-diagnostico", execucao_token, metodo="POST")
    aguardar_saga(ordens_token, ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{execucao_id}/concluir-diagnostico", execucao_token,
           {"diagnostico": "Sem itens para cobrar", "servicoIds": [], "pecasInsumos": []}, "POST")
    saga = aguardar_saga(ordens_token, ordem["id"], "FalhaRequerIntervencao")
    assert saga["orcamentoId"] is None and saga["motivo"]
    assert chamar(COBRANCA, f"/api/orcamentos?ordemServicoId={ordem['id']}", cobranca_token) == []
    assert chamar(ORDENS, f"/api/ordensservico/{ordem['id']}", ordens_token)["status"] == "EmDiagnostico"
    print("OK: Cobranca recusou diagnostico sem itens; Saga registrou motivo e nao avancou a OS")


if __name__ == "__main__":
    main()
