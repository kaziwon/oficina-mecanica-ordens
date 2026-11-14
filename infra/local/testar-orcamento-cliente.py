"""Consulta, aprovacao e recusa pelo cliente usando as APIs e a Saga reais locais."""

import uuid
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, autenticar, criar_ordem, aguardar_saga, preparar_orcamento


def login_cliente(admin, ordem):
    cadastro = chamar(ORDENS, f"/api/clientes/{ordem['clienteId']}", admin)
    return chamar(ORDENS, "/api/auth/cliente", body={"cpf": cadastro["cpfCnpj"]}, metodo="POST")["token"]


def main():
    o, e, c = autenticar(ORDENS), autenticar(EXECUCAO), autenticar(COBRANCA)
    outro = login_cliente(o, criar_ordem(o))
    for aprovado in (True, False):
        servico = chamar(EXECUCAO, "/api/servicos", e, {"nome": "Orcamento cliente " + str(uuid.uuid4()), "preco": 100}, "POST")
        ordem, inicial = preparar_orcamento(o, e, servico["id"])
        cliente = login_cliente(o, ordem)
        rota = f"/api/minhas-ordens/{ordem['id']}/orcamento"
        orcamento = chamar(ORDENS, rota, cliente)
        assert orcamento["id"] == inicial["orcamentoId"] and orcamento["status"] == "AguardandoAprovacao"
        assert orcamento["valorTotal"] == 100 and len(orcamento["itens"]) == 1
        for token in (None, o):
            chamar(ORDENS, rota, token, status_esperado=401)
        chamar(ORDENS, rota, outro, status_esperado=404)
        chamar(ORDENS, rota + "/aprovar", outro, metodo="POST", status_esperado=404)
        chamar(ORDENS, rota + "/recusar", outro, {"motivo": "Nao sou o dono"}, "POST", 404)
        chamar(COBRANCA, f"/internal/orcamentos/{orcamento['id']}", cliente, status_esperado=401)
        chamar(COBRANCA, f"/internal/orcamentos/{orcamento['id']}", c, status_esperado=401)
        chamar(ORDENS, rota + "/aprovar", cliente, {"valorTotal": 1}, "POST", 400)
        chamar(ORDENS, rota + "/recusar", cliente, {"motivo": "\n"}, "POST", 400)
        sufixo = "/aprovar" if aprovado else "/recusar"
        body = None if aprovado else {"motivo": "Valor fora do meu orcamento"}
        primeira = chamar(ORDENS, rota + sufixo, cliente, body, "POST")
        repetida = chamar(ORDENS, rota + sufixo, cliente, body, "POST")
        assert primeira == repetida and primeira["respondidoEm"]
        assert primeira["status"] == ("Aprovado" if aprovado else "Recusado") and primeira["valorTotal"] == 100
        oposto = "/recusar" if aprovado else "/aprovar"
        chamar(ORDENS, rota + oposto, cliente, {"motivo": "Contradicao"} if aprovado else None, "POST", 409)
        saga = aguardar_saga(o, ordem["id"], "ExecucaoLiberada" if aprovado else "OrcamentoRecusado")
        registro = chamar(COBRANCA, f"/api/orcamentos/{orcamento['id']}", c)
        assert len(registro["historico"]) == 2 and registro["valorTotal"] == 100
        consulta_os = chamar(ORDENS, f"/api/minhas-ordens/{ordem['id']}", cliente)
        assert consulta_os["status"] == ("AguardandoExecucao" if aprovado else "Cancelada")
        if aprovado:
            pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", c)
            assert pagamento["status"] == "Confirmado" and pagamento["tentativas"] == 1
            assert chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}", e)["status"] == "AguardandoExecucao"
        else:
            assert registro["pagamentoId"] is None and saga["pagamentoId"] is None
            assert chamar(EXECUCAO, f"/api/execucoes/{saga['execucaoId']}", e)["status"] == "Cancelada"
        assert chamar(ORDENS, rota, cliente)["status"] == primeira["status"]
        print(f"OK: OS {ordem['id']}: cliente {'aprovou' if aprovado else 'recusou'}, repeticao idempotente e Saga atualizada")
    print("OK: acesso cruzado, rotas internas, alteracao de valor e decisoes contraditorias bloqueados")
    print("OK: aprovacao gerou uma cobranca mock; recusa cancelou sem cobranca ou reparo automatico")


if __name__ == "__main__":
    main()
