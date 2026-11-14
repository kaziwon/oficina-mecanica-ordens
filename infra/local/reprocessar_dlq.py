"""Inspeciona ou reenvia mensagens das DLQs locais, sem usar credenciais da AWS."""

import argparse
import json
import sys
import uuid
from datetime import datetime
from urllib.parse import urlparse

TIPOS = {
    "execucao-comandos": {"RegistrarExecucao", "CancelarEntradaExecucao", "CancelarExecucaoPorRecusaOrcamento",
                          "LiberarExecucao", "CancelarReparoPago"},
    "cobranca-comandos": {"GerarOrcamento", "SolicitarPagamento", "SolicitarEstorno"},
    "ordens-eventos": {"ExecucaoRegistrada", "RegistroExecucaoRecusado", "EntradaExecucaoCancelada",
                       "CancelamentoEntradaExecucaoRecusado", "DiagnosticoIniciado", "DiagnosticoConcluido",
                       "OrcamentoGerado", "GeracaoOrcamentoRecusada", "OrcamentoAprovado", "OrcamentoRecusado",
                       "ExecucaoCanceladaPorRecusaOrcamento", "CancelamentoPorRecusaOrcamentoRecusado",
                       "PagamentoConfirmado", "PagamentoRecusado", "FalhaProcessamentoPagamento",
                       "ExecucaoLiberada", "LiberacaoExecucaoRecusada", "ReparoIniciado", "ReparoConcluido",
                       "ReparoPagoCancelado", "CancelamentoReparoPagoRecusado",
                       "EstornoConfirmado", "EstornoRecusado", "FalhaProcessamentoEstorno"},
}
CAMPOS = {"messageId", "type", "version", "sagaId", "causationId", "occurredAt", "correlationId", "payload"}


def validar_endpoint(endpoint):
    url = urlparse(endpoint)
    if (url.scheme != "http" or url.hostname not in ("localhost", "127.0.0.1", "::1")
            or url.username or url.password or url.query or url.fragment):
        raise ValueError("Use somente o endpoint HTTP do ElasticMQ local, por exemplo http://localhost:9324.")


def validar_envelope(body, origem):
    if len(body.encode("utf-8")) > 262144:
        raise ValueError("Envelope muito grande.")
    def objeto_unico(pares):
        resultado = {}
        for chave, valor in pares:
            if chave in resultado:
                raise ValueError("Campo JSON duplicado.")
            resultado[chave] = valor
        return resultado
    mensagem = json.loads(body, object_pairs_hook=objeto_unico)
    if not isinstance(mensagem, dict) or set(mensagem) != CAMPOS:
        raise ValueError("Envelope incompleto ou com campos desconhecidos.")
    if type(mensagem["version"]) is not int or mensagem["version"] != 1 or mensagem["type"] not in TIPOS[origem]:
        raise ValueError("Tipo ou versao nao aceitos para esta fila.")
    for campo in ("messageId", "sagaId"):
        if uuid.UUID(mensagem[campo]).int == 0:
            raise ValueError("Identificador vazio.")
    if mensagem["causationId"] is not None and uuid.UUID(mensagem["causationId"]).int == 0:
        raise ValueError("Causa vazia.")
    correlacao = mensagem["correlationId"]
    if not isinstance(correlacao, str) or not correlacao.strip() or len(correlacao) > 100 or any(ord(c) < 32 for c in correlacao):
        raise ValueError("Correlacao invalida.")
    data = datetime.fromisoformat(mensagem["occurredAt"].replace("Z", "+00:00"))
    if data.tzinfo is None or not isinstance(mensagem["payload"], dict):
        raise ValueError("Data ou payload invalido.")
    if uuid.UUID(mensagem["payload"]["ordemServicoId"]).int == 0:
        raise ValueError("OS vazia.")
    return mensagem


def reprocessar(cliente, origem, confirmar=False, limite=1, emitir=print):
    if origem not in TIPOS or type(limite) is not int or not 1 <= limite <= 10:
        raise ValueError("Fila ou limite invalido.")
    destino = cliente.get_queue_url(QueueName=origem)["QueueUrl"]
    dlq = cliente.get_queue_url(QueueName=origem + "-dlq")["QueueUrl"]
    validar_endpoint(destino); validar_endpoint(dlq)
    mensagens = cliente.receive_message(QueueUrl=dlq, MaxNumberOfMessages=limite, WaitTimeSeconds=1,
                                       VisibilityTimeout=30).get("Messages", [])
    resultado = {"recebidas": len(mensagens), "reenviadas": 0, "falhas": 0}
    for item in mensagens:
        reenviada = False
        try:
            envelope = validar_envelope(item["Body"], origem)
            # Nao registrar payload, CPF, tokens ou dados pessoais do envelope.
            emitir(f"{envelope['messageId']} {envelope['type']}: " + ("reenvio" if confirmar else "somente inspecao"))
            if confirmar:
                resposta = cliente.send_message(QueueUrl=destino, MessageBody=item["Body"])
                if not resposta.get("MessageId"):
                    raise RuntimeError("Envio sem confirmacao.")
                reenviada = True
                cliente.delete_message(QueueUrl=dlq, ReceiptHandle=item["ReceiptHandle"])
                resultado["reenviadas"] += 1
        except Exception as erro:
            resultado["falhas"] += 1
            emitir(f"Mensagem mantida na DLQ ({type(erro).__name__}); "
                   + ("pode ter sido reenviada, repetir preserva o ID." if reenviada else "nenhuma exclusao confirmada."))
        finally:
            if not confirmar or not reenviada:
                cliente.change_message_visibility(QueueUrl=dlq, ReceiptHandle=item["ReceiptHandle"], VisibilityTimeout=0)
    return resultado


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fila", choices=sorted(TIPOS), required=True, help="Fila original, sem o sufixo -dlq.")
    parser.add_argument("--endpoint", default="http://localhost:9324")
    parser.add_argument("--limite", type=int, choices=range(1, 11), default=1)
    parser.add_argument("--confirmar", action="store_true", help="Reenviar; por padrao somente inspeciona.")
    args = parser.parse_args()
    validar_endpoint(args.endpoint)
    import boto3
    from botocore.config import Config
    cliente = boto3.client("sqs", endpoint_url=args.endpoint, region_name="us-east-1",
                           aws_access_key_id="local", aws_secret_access_key="local",
                           config=Config(connect_timeout=3, read_timeout=5, retries={"max_attempts": 0}))
    try:
        resultado = reprocessar(cliente, args.fila, args.confirmar, args.limite)
        print(json.dumps(resultado)); return 1 if resultado["falhas"] else 0
    finally:
        cliente.close()


if __name__ == "__main__":
    sys.exit(main())
