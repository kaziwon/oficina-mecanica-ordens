import uuid
from behave import given, when, then
from saga_http import ORDENS, EXECUCAO, COBRANCA, chamar, criar_ordem, aguardar_saga


@given("que uma OS tem diagnostico e orcamento com servico de 100 e duas pecas de 50")
def diagnosticar(context):
    servico = chamar(EXECUCAO, "/api/servicos", context.e,
                     {"nome": "BDD revisao " + str(uuid.uuid4()), "preco": 100}, "POST")
    context.peca = chamar(EXECUCAO, "/api/pecas", context.e,
                         {"nome": "BDD filtro " + str(uuid.uuid4()), "precoUnitario": 50, "quantidadeEstoque": 10}, "POST")
    context.ordem = criar_ordem(context.o)
    context.saga = aguardar_saga(context.o, context.ordem["id"], "RegistroConfirmado")
    context.execucao_id = context.saga["execucaoId"]
    cadastro = chamar(ORDENS, f"/api/clientes/{context.ordem['clienteId']}", context.o)
    context.cliente = chamar(ORDENS, "/api/auth/cliente", body={"cpf": cadastro["cpfCnpj"]}, metodo="POST")["token"]
    context.rota_cliente = f"/api/minhas-ordens/{context.ordem['id']}/orcamento"
    chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}/iniciar-diagnostico", context.e, metodo="POST")
    aguardar_saga(context.o, context.ordem["id"], "EmDiagnostico")
    chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}/concluir-diagnostico", context.e,
           {"diagnostico": "Trocar filtros", "servicoIds": [servico["id"]],
            "pecasInsumos": [{"pecaInsumoId": context.peca["id"], "quantidade": 2}]}, "POST")
    context.saga = aguardar_saga(context.o, context.ordem["id"], "OrcamentoGerado")
    orcamento = chamar(ORDENS, context.rota_cliente, context.cliente)
    assert orcamento["valorTotal"] == 200 and len(orcamento["itens"]) == 2


@when("o proprio cliente aprova seu orcamento")
def aprovar(context):
    chamar(ORDENS, context.rota_cliente + "/aprovar", context.cliente, metodo="POST")


@then("a Saga confirma um pagamento de 200 e libera a execucao")
def confirmar_pagamento(context):
    context.saga = aguardar_saga(context.o, context.ordem["id"], "ExecucaoLiberada")
    context.pagamento = chamar(COBRANCA, f"/api/pagamentos/{context.saga['pagamentoId']}", context.c)
    assert context.pagamento["status"] == "Confirmado" and context.pagamento["valor"] == 200
    assert context.pagamento["tentativas"] == 1
    assert chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}", context.e)["status"] == "AguardandoExecucao"


def iniciar_reparo(context):
    chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}/iniciar-reparo", context.e, metodo="POST")
    aguardar_saga(context.o, context.ordem["id"], "EmExecucao")
    assert chamar(EXECUCAO, f"/api/pecas/{context.peca['id']}", context.e)["quantidadeReservada"] == 2


@when("o mecanico inicia e conclui o reparo")
def executar(context):
    iniciar_reparo(context)
    chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}/concluir-reparo", context.e, metodo="POST")


@then("a OS fica finalizada e o estoque tem 8 pecas sem reservas")
def finalizar(context):
    aguardar_saga(context.o, context.ordem["id"], "Finalizada")
    assert chamar(ORDENS, f"/api/minhas-ordens/{context.ordem['id']}", context.cliente)["status"] == "Finalizada"
    estoque = chamar(EXECUCAO, f"/api/pecas/{context.peca['id']}", context.e)
    assert estoque["quantidadeEstoque"] == 8 and estoque["quantidadeReservada"] == 0


@when("o atendente registra a entrega duas vezes")
def entregar(context):
    rota = f"/api/ordensservico/{context.ordem['id']}/entregar"
    primeira = chamar(ORDENS, rota, context.o, metodo="POST")
    segunda = chamar(ORDENS, rota, context.o, metodo="POST")
    assert primeira["historico"] == segunda["historico"]


@then("o cliente ve a OS entregue com um unico registro de entrega")
def verificar_entrega(context):
    ordem = chamar(ORDENS, f"/api/minhas-ordens/{context.ordem['id']}", context.cliente)
    assert ordem["status"] == "Entregue"
    assert len([item for item in ordem["historico"] if item["status"] == "Entregue"]) == 1
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{context.pagamento['id']}", context.c)
    assert pagamento["status"] == "Confirmado" and pagamento["tentativas"] == 1


@when("o proprio cliente recusa seu orcamento")
def recusar(context):
    chamar(ORDENS, context.rota_cliente + "/recusar", context.cliente, {"motivo": "Fora do meu orcamento"}, "POST")


@then("a OS e a execucao ficam canceladas sem pagamento nem consumo de estoque")
def verificar_recusa(context):
    saga = aguardar_saga(context.o, context.ordem["id"], "OrcamentoRecusado")
    assert saga["pagamentoId"] is None
    assert chamar(ORDENS, f"/api/minhas-ordens/{context.ordem['id']}", context.cliente)["status"] == "Cancelada"
    assert chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}", context.e)["status"] == "Cancelada"
    orcamento = chamar(COBRANCA, f"/api/orcamentos/{saga['orcamentoId']}", context.c)
    assert orcamento["pagamentoId"] is None
    estoque = chamar(EXECUCAO, f"/api/pecas/{context.peca['id']}", context.e)
    assert estoque["quantidadeEstoque"] == 10 and estoque["quantidadeReservada"] == 0


@when("o mecanico inicia o reparo e o cliente solicita o cancelamento ao atendente")
def cancelar(context):
    iniciar_reparo(context)
    chamar(ORDENS, f"/api/ordensservico/{context.ordem['id']}/cancelar-reparo", context.o,
           {"motivo": "Desistencia durante o reparo"}, "POST", 202)


@then("a Saga confirma um estorno de 200 e cancela a OS preservando a cobranca original")
def verificar_estorno(context):
    saga = aguardar_saga(context.o, context.ordem["id"], "CompensadaAposPagamento")
    assert chamar(ORDENS, f"/api/minhas-ordens/{context.ordem['id']}", context.cliente)["status"] == "Cancelada"
    estorno = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}/estorno", context.c)
    assert estorno["status"] == "Confirmado" and estorno["valor"] == 200 and estorno["id"] == saga["estornoId"]
    pagamento = chamar(COBRANCA, f"/api/pagamentos/{saga['pagamentoId']}", context.c)
    assert pagamento["id"] == context.pagamento["id"] and pagamento["status"] == "Confirmado" and pagamento["tentativas"] == 1


@then("as pecas reservadas sao liberadas sem repor estoque consumido")
def verificar_reservas(context):
    estoque = chamar(EXECUCAO, f"/api/pecas/{context.peca['id']}", context.e)
    assert estoque["quantidadeEstoque"] == 10 and estoque["quantidadeReservada"] == 0
    execucao = chamar(EXECUCAO, f"/api/execucoes/{context.execucao_id}", context.e)
    assert execucao["status"] == "Cancelada"
    assert len(execucao["reservasPecas"]) == 1
    assert execucao["reservasPecas"][0]["liberadaEm"] and not execucao["reservasPecas"][0]["consumidaEm"]
