import os
import unittest
import uuid
from test_reprocessar_dlq import envelope
from reprocessar_dlq import reprocessar, validar_endpoint


@unittest.skipUnless(os.getenv("SQS_TEST_ENDPOINT"), "Informe o ElasticMQ local em SQS_TEST_ENDPOINT.")
class ElasticMqTests(unittest.TestCase):
    def test_inspecao_e_reenvio_real_preservam_body_e_id(self):
        import boto3
        from botocore.config import Config
        endpoint = os.environ["SQS_TEST_ENDPOINT"]; validar_endpoint(endpoint)
        cliente = boto3.client("sqs", endpoint_url=endpoint, region_name="us-east-1",
                               aws_access_key_id="local", aws_secret_access_key="local",
                               config=Config(connect_timeout=3, read_timeout=5, retries={"max_attempts": 0}))
        prefixo = "teste-replay-" + uuid.uuid4().hex
        urls = {}
        try:
            for nome in ("execucao-comandos", "execucao-comandos-dlq"):
                urls[nome] = cliente.create_queue(QueueName=prefixo + "-" + nome)["QueueUrl"]
            class FilasIsoladas:
                def get_queue_url(self, QueueName): return {"QueueUrl": urls[QueueName]}
                def __getattr__(self, nome): return getattr(cliente, nome)
            body = envelope(); cliente.send_message(QueueUrl=urls["execucao-comandos-dlq"], MessageBody=body)
            inspecao = reprocessar(FilasIsoladas(), "execucao-comandos", emitir=lambda _: None)
            self.assertEqual(1, inspecao["recebidas"]); self.assertEqual(0, inspecao["reenviadas"])
            resultado = reprocessar(FilasIsoladas(), "execucao-comandos", True, emitir=lambda _: None)
            self.assertEqual(1, resultado["reenviadas"])
            recebida = cliente.receive_message(QueueUrl=urls["execucao-comandos"], WaitTimeSeconds=1)["Messages"][0]
            self.assertEqual(body, recebida["Body"])
            self.assertFalse(cliente.receive_message(QueueUrl=urls["execucao-comandos-dlq"], WaitTimeSeconds=1).get("Messages"))
        finally:
            for url in urls.values(): cliente.delete_queue(QueueUrl=url)
            cliente.close()


if __name__ == "__main__":
    unittest.main()
