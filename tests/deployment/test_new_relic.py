import json
import sys
import tempfile
import unittest
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from new_relic_config import read_new_relic


class NewRelicTests(unittest.TestCase):
    def test_ausencia_de_configuracao_nao_habilita_envio(self):
        with tempfile.TemporaryDirectory() as folder:
            home = Path(folder)
            self.assertEqual({"enabled": False, "license_key": ""}, read_new_relic(home))
            self.assertFalse((home / "secrets/newrelic.json").exists())

    def test_configuracao_habilitada_exige_key_valida_e_privada(self):
        with tempfile.TemporaryDirectory() as folder:
            home = Path(folder)
            path = home / "secrets/newrelic.json"
            path.parent.mkdir()
            path.write_text(json.dumps({"enabled": True, "license_key": "chave-ficticia-somente-teste"}))
            self.assertTrue(read_new_relic(home)["enabled"])
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
            for settings in ({"enabled": "true", "license_key": "a"*40},
                             {"enabled": True, "license_key": ""},
                             {"enabled": True, "license_key": "a"*40 + "\\nexterno"}):
                path.write_text(json.dumps(settings))
                with self.assertRaises(ValueError):
                    read_new_relic(home)

    def test_desabilitado_nao_injeta_key_retida(self):
        with tempfile.TemporaryDirectory() as folder:
            home = Path(folder)
            path = home / "secrets/newrelic.json"
            path.parent.mkdir()
            path.write_text(json.dumps({"enabled": False, "license_key": "chave-ficticia-somente-teste"}))
            self.assertEqual("", read_new_relic(home)["license_key"])

    def test_instrumentacao_xml_aponta_para_metodos_finitos_da_api(self):
        root = ElementTree.parse(ROOT / "observability/newrelic-instrumentation.xml").getroot()
        ns = {"nr": "urn:newrelic-extension"}
        matches = root.findall(".//nr:match", ns)
        self.assertEqual(2, len(matches))
        worker = (ROOT / "src/OficinaMecanica.Ordens.Api/Messaging/IntegracaoWorker.cs").read_text()
        for match in matches:
            self.assertEqual("OficinaMecanica.Ordens.Api", match.attrib["assemblyName"])
            self.assertEqual("OficinaMecanica.Ordens.Messaging.IntegracaoWorker", match.attrib["className"])
            for method in match.findall("nr:exactMethodMatcher", ns):
                self.assertNotEqual("ExecuteAsync", method.attrib["methodName"])
                self.assertIn("Task " + method.attrib["methodName"] + "(", worker)

    def test_imagem_tem_agente_fixado_desabilitado_por_padrao_sem_chave(self):
        dockerfile = (ROOT / "Dockerfile").read_text()
        for setting in ("10.57.0", "sha256sum --check --strict", "CORECLR_ENABLE_PROFILING=0",
                        "NEW_RELIC_LOG_DIRECTORY=/tmp/newrelic", "NEW_RELIC_TRANSACTION_TRACER_RECORD_SQL=off"):
            self.assertIn(setting, dockerfile)
        self.assertNotIn("NEW_RELIC_LICENSE_KEY", dockerfile)
        self.assertIn("/usr/local/newrelic-dotnet-agent/extensions/oficina.xml", dockerfile)
        for layer in ("Domain", "Application", "InterfaceAdapters"):
            for project in (ROOT / "src").glob("*." + layer + "/*.csproj"):
                self.assertNotIn("NewRelic", project.read_text())

    def test_sdk_v4_disponibiliza_carrier_de_headers(self):
        transport = (ROOT / "src/OficinaMecanica.Ordens.Infrastructure/Messaging/SqsTransport.cs").read_text()
        self.assertIn("MessageAttributes = []", transport)

    def test_profiler_tem_diagnostico_e_logs_compativeis_com_container_readonly(self):
        config = (ROOT / "infra/kubernetes/api.tf").read_text()
        for name in ("DOTNET_EnableDiagnostics", "CORECLR_ENABLE_PROFILING"):
            line = next(line for line in config.splitlines() if name in line)
            self.assertIn('var.new_relic_enabled ? "1" : "0"', line)
        for name in ("NEW_RELIC_LOG_CONSOLE", "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED",
                     "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED"):
            line = next(line for line in config.splitlines() if name + " " in line)
            self.assertIn('= "true"', line)

    def test_migrations_nao_iniciam_profiler(self):
        path = ROOT / "k8s/migrations-job.yaml.tftpl"
        if path.exists():
            self.assertIn("{name: CORECLR_ENABLE_PROFILING, value: '0'}", path.read_text())
            self.assertIn("{name: NEW_RELIC_AGENT_ENABLED, value: 'false'}", path.read_text())


if __name__ == "__main__":
    unittest.main()
