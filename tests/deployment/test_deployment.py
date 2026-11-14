import json
import os
import stat
import sys
import tempfile
import unittest
from pathlib import Path
from string import Template
from types import SimpleNamespace
from unittest.mock import patch

import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from cd_gate import validate_runs
from deploy_local import deploy
from deploy_settings import IMAGE_BUILDS, new_private_secrets, service_values
from local_runtime import CONTEXT, kube, operation_lock, private_json, require_cluster, runtime_home

SHA = "a" * 40
REPOSITORY = "kaziwon/servico"


def approved(**changes):
    return {"head_sha": SHA, "head_branch": "main", "head_repository": {"full_name": REPOSITORY},
            "run_number": 1, "run_attempt": 1, "status": "completed", "conclusion": "success",
            "event": "push", **changes}


def manifest(name):
    config = json.loads((ROOT / "k8s" / "deployment.json").read_text())
    values = {"namespace": "oficina-" + config["service"], "node_port": config["node_port"],
              "api_image": "imagem:sha-unico", "mock_image": "mock:sha-unico", "job_name": "init-teste",
              "config_checksum": "a" * 64, "secret_checksum": "b" * 64, "rollout_checksum": "c" * 64}
    return yaml.safe_load(Template((ROOT / "k8s" / name).read_text()).safe_substitute(values))


class DeploymentGateTests(unittest.TestCase):
    def test_aceita_somente_revisao_aprovada(self):
        validate_runs([approved()], SHA, REPOSITORY)

    def test_rejeita_falha_ou_verificacao_incompleta(self):
        for changes in ({"conclusion": "failure"}, {"conclusion": "skipped"}, {"status": "in_progress"}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                validate_runs([approved(**changes)], SHA, REPOSITORY)

    def test_rejeita_sha_branch_fork_e_pr(self):
        for changes in ({"head_sha": "b" * 40}, {"head_branch": "feature/teste"},
                        {"head_repository": {"full_name": "outro/fork"}}, {"event": "pull_request"},
                        {"head_repository": None}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                validate_runs([approved(**changes)], SHA, REPOSITORY)

    def test_reexecucao_mais_nova_com_falha_nao_e_ocultada(self):
        with self.assertRaises(ValueError):
            validate_runs([approved(), approved(run_attempt=2, conclusion="failure")], SHA, REPOSITORY)

    def test_workflow_local_nao_executa_pr_e_usa_sha_exato(self):
        workflow = yaml.load((ROOT / ".github/workflows/entrega-continua-local.yml").read_text(), Loader=yaml.BaseLoader)
        self.assertEqual({"workflow_run", "workflow_dispatch"}, set(workflow["on"]))
        job = workflow["jobs"]["deploy"]
        self.assertEqual(["self-hosted", "macOS", "kind-local"], job["runs-on"])
        self.assertIn("github.event.workflow_run.head_repository.full_name == github.repository", job["if"])
        self.assertIn("github.ref == 'refs/heads/main'", job["if"])
        checkout = next(step for step in job["steps"] if step.get("uses", "").startswith("actions/checkout@"))
        self.assertIn("github.event.workflow_run.head_sha", checkout["with"]["ref"])
        self.assertEqual("false", checkout["with"]["persist-credentials"])
        self.assertTrue(any("--ci --sha" in step.get("run", "") for step in job["steps"]))
        quality = yaml.load((ROOT / ".github/workflows/qualidade-local.yml").read_text(), Loader=yaml.BaseLoader)
        self.assertIn("github.sha == github.event.workflow_run.head_sha", quality["jobs"]["sonar"]["if"])


class RuntimeSafetyTests(unittest.TestCase):
    def test_configuracao_nao_pode_redirecionar_deploy_para_outro_servico(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "OficinaMecanica.Ordens.slnx").touch()
            (root / "k8s").mkdir()
            (root / "k8s/deployment.json").write_text(json.dumps({
                "service": "execucao", "solution": "OficinaMecanica.Ordens.slnx"}))
            with patch("deploy_local.runtime_home", side_effect=AssertionError("Nao deve alterar o ambiente")), self.assertRaises(ValueError):
                deploy(root, SimpleNamespace())

    def test_senhas_sao_persistentes_e_privadas(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "credenciais.json"
            first = private_json(path, lambda: {"value": "teste"})
            second = private_json(path, lambda: {"value": "nao-substituir"})
            self.assertEqual(first, second)
            self.assertEqual(0o600, stat.S_IMODE(path.stat().st_mode))

    def test_nao_regenera_senha_se_ja_existe_volume(self):
        with tempfile.TemporaryDirectory() as folder, self.assertRaises(ValueError):
            private_json(Path(folder) / "ausente.json", lambda: {}, existing_resources=True)

    def test_contexto_kubectl_e_sempre_explicito(self):
        with patch("local_runtime.run") as execute:
            kube(Path("/tmp/config com espacos"), "get", "nodes")
        self.assertEqual(["kubectl", "--kubeconfig=/tmp/config com espacos/kubeconfig", "--context=" + CONTEXT,
                          "get", "nodes"], execute.call_args.args[0])

    def test_rejeita_cluster_externo(self):
        with tempfile.TemporaryDirectory() as folder:
            home = Path(folder)
            (home / "kubeconfig").touch()
            result = SimpleNamespace(stdout=json.dumps({"clusters": [{"cluster": {"server": "https://producao.example.com"}}]}))
            with patch("local_runtime.shutil.which", return_value="tool"), patch("local_runtime.kube", return_value=result), self.assertRaises(ValueError):
                require_cluster(home)

    def test_runtime_nao_pode_ser_relativo(self):
        with patch.dict(os.environ, {"OFICINA_HOME": "relativo"}), self.assertRaises(ValueError):
            runtime_home()

    def test_trava_local_impede_operacoes_concorrentes_e_libera_apos_saida(self):
        with tempfile.TemporaryDirectory() as folder:
            home = Path(folder)
            with operation_lock(home):
                with self.assertRaises(TimeoutError):
                    with operation_lock(home, timeout=0):
                        pass
            with operation_lock(home, timeout=0):
                pass


class ManifestTests(unittest.TestCase):
    def test_templates_yaml_validos_no_namespace_do_proprio_servico(self):
        config = json.loads((ROOT / "k8s/deployment.json").read_text())
        for path in (ROOT / "k8s").glob("*.yaml.tftpl"):
            with self.subTest(path=path.name):
                data = manifest(path.name)
                self.assertEqual("oficina-" + config["service"], data["metadata"]["namespace"])
                self.assertIn(data["kind"], ("Service", "StatefulSet", "Deployment", "Job"))

    def test_api_tem_probes_limites_secrets_e_nao_recebe_token_kubernetes(self):
        pod = manifest("api-deployment.yaml.tftpl")["spec"]["template"]["spec"]
        self.assertFalse(pod["automountServiceAccountToken"])
        container = pod["containers"][0]
        for field in ("readinessProbe", "livenessProbe", "startupProbe", "resources"):
            self.assertIn(field, container)
        self.assertIn({"secretRef": {"name": "api-secrets"}}, container["envFrom"])
        self.assertTrue(container["securityContext"]["readOnlyRootFilesystem"])

    def test_banco_nao_expoe_nodeport(self):
        for path in (ROOT / "k8s").glob("*-service.yaml.tftpl"):
            if path.name == "api-service.yaml.tftpl":
                continue
            data = manifest(path.name)
            self.assertEqual("ClusterIP", data["spec"]["type"])

    def test_aplicacao_nao_usa_a_senha_root_do_banco(self):
        common = {"jwt_secret": "jwt-de-teste", "admin_password": "admin-de-teste", "ordens_cobranca_secret": "integracao-de-teste"}
        private = new_private_secrets()
        values = service_values(common, private)
        self.assertNotIn(private["database_root_password"], json.dumps(values["api_secrets"]))
        self.assertIn("api_image", IMAGE_BUILDS)

    def test_migrations_terminam_sem_subir_api_ou_mongo_tem_bootstrap(self):
        if (ROOT / "k8s/migrations-job.yaml.tftpl").exists():
            job = manifest("migrations-job.yaml.tftpl")
            env = {item["name"]: item["value"] for item in job["spec"]["template"]["spec"]["containers"][0]["env"]}
            self.assertEqual("true", env["Database__ApplyMigrations"])
            self.assertEqual("true", env["Database__MigrateOnly"])
        else:
            job = manifest("mongo-init-job.yaml.tftpl")
            self.assertEqual("Job", job["kind"])
            script = (ROOT / "k8s/mongo-replica-init.js").read_text()
            self.assertIn("rs.initiate", script)
            self.assertIn("isWritablePrimary", script)


if __name__ == "__main__":
    unittest.main()
