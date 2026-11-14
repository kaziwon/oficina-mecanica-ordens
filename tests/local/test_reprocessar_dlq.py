import json
import sys
import unittest
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "infra" / "local"))
from reprocessar_dlq import reprocessar, validar_endpoint, validar_envelope


def envelope():
    return json.dumps({"messageId": str(uuid.uuid4()), "type": "RegistrarExecucao", "version": 1,
                       "sagaId": str(uuid.uuid4()), "causationId": None, "occurredAt": "2026-11-13T10:00:00Z",
                       "correlationId": "teste-dlq", "payload": {"ordemServicoId": str(uuid.uuid4()),
                       "descricaoSolicitacao": "Revisao"}}, indent=2)


class FilaFake:
    def __init__(self, body=None, falha=None):
        self.body = body or envelope(); self.falha = falha; self.acoes = []; self.enviadas = []

    def get_queue_url(self, QueueName):
        return {"QueueUrl": "http://localhost:9324/000000000000/" + QueueName}

    def receive_message(self, **kwargs):
        return {"Messages": [{"Body": self.body, "ReceiptHandle": "recibo"}]}

    def send_message(self, **kwargs):
        self.acoes.append("enviar")
        if self.falha == "envio": raise RuntimeError("Provedor indisponivel")
        self.enviadas.append(kwargs["MessageBody"])
        return {} if self.falha == "sem_ack" else {"MessageId": "id-sqs-novo"}

    def delete_message(self, **kwargs):
        self.acoes.append("apagar")
        if self.falha == "exclusao": raise RuntimeError("ACK de exclusao perdido")

    def change_message_visibility(self, **kwargs):
        self.acoes.append("visivel")


class ReprocessamentoTests(unittest.TestCase):
    def test_inspecao_nao_envia_nem_apaga(self):
        fila = FilaFake()
        resultado = reprocessar(fila, "execucao-comandos", emitir=lambda _: None)
        self.assertEqual(["visivel"], fila.acoes); self.assertEqual(0, resultado["reenviadas"])

    def test_preserva_body_exato_e_apaga_somente_depois_do_ack(self):
        fila = FilaFake()
        resultado = reprocessar(fila, "execucao-comandos", True, emitir=lambda _: None)
        self.assertEqual(["enviar", "apagar"], fila.acoes); self.assertEqual([fila.body], fila.enviadas)
        self.assertEqual(1, resultado["reenviadas"])

    def test_falha_envio_ou_ack_nao_apaga(self):
        for falha in ("envio", "sem_ack"):
            with self.subTest(falha=falha):
                fila = FilaFake(falha=falha)
                resultado = reprocessar(fila, "execucao-comandos", True, emitir=lambda _: None)
                self.assertNotIn("apagar", fila.acoes); self.assertEqual(1, resultado["falhas"])

    def test_falha_exclusao_nao_altera_identidade_no_reenvio(self):
        fila = FilaFake(falha="exclusao")
        resultado = reprocessar(fila, "execucao-comandos", True, emitir=lambda _: None)
        self.assertEqual(1, resultado["falhas"]); self.assertEqual([fila.body], fila.enviadas)

    def test_envelope_invalido_fica_na_dlq(self):
        for campo, valor in (("version", 2), ("type", "SolicitarPagamento"), ("messageId", str(uuid.UUID(int=0))),
                             ("payload", None), ("correlationId", "\n"), ("occurredAt", "2026-11-13")):
            with self.subTest(campo=campo):
                body = json.loads(envelope()); body[campo] = valor
                fila = FilaFake(json.dumps(body)); resultado = reprocessar(fila, "execucao-comandos", True, emitir=lambda _: None)
                self.assertEqual(["visivel"], fila.acoes); self.assertEqual(1, resultado["falhas"])

    def test_rejeita_aws_campos_duplicados_e_fila_desconhecida(self):
        for url in ("https://sqs.us-east-1.amazonaws.com", "http://example.com", "http://user@localhost:9324"):
            with self.assertRaises(ValueError): validar_endpoint(url)
        with self.assertRaises(ValueError): validar_envelope('{"version":1,"version":1}', "execucao-comandos")
        with self.assertRaises(ValueError): reprocessar(FilaFake(), "outra-fila")


if __name__ == "__main__":
    unittest.main()
