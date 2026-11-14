"""Operacoes locais compartilhadas pelo deploy, sem dependencias de nuvem."""

import hashlib
import contextlib
import fcntl
import json
import os
import platform
import secrets
import shutil
import subprocess
import tempfile
import time
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import urlopen

CLUSTER = "oficina-fase4"
CONTEXT = "kind-" + CLUSTER
KIND_VERSION = "v0.33.0"
KIND_BINARIES = {
    ("Darwin", "arm64"): ("darwin-arm64", "0c8c7dbe5e23594a198b786c4bc13dacc101fa6196b0cb0b23a1ca44e61f4b4f"),
}


@contextlib.contextmanager
def operation_lock(home, timeout=1800):
    directory = home / "locks"
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    with (directory / "operations.lock").open("a+") as stream:
        os.chmod(stream.name, 0o600)
        deadline = time.monotonic() + timeout
        announced = False
        while True:
            try:
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if time.monotonic() >= deadline:
                    raise TimeoutError("Outra operacao local ainda esta usando os recursos do notebook.")
                if not announced:
                    print("Aguardando outra analise/deploy terminar no notebook...", flush=True)
                    announced = True
                time.sleep(2)
        try:
            yield
        finally:
            fcntl.flock(stream, fcntl.LOCK_UN)


def run(args, *, capture=False, env=None, check=True):
    return subprocess.run([str(arg) for arg in args], check=check, text=True,
                          capture_output=capture, env=env)


def runtime_home():
    path = Path(os.environ.get("OFICINA_HOME", "~/.local/share/oficina-fase4")).expanduser()
    if not path.is_absolute():
        raise ValueError("OFICINA_HOME deve ser um caminho absoluto.")
    path.mkdir(parents=True, exist_ok=True, mode=0o700)
    path.chmod(0o700)
    return path


def kube(home, *args, capture=False, check=True):
    return run(["kubectl", "--kubeconfig=" + str(home / "kubeconfig"),
                "--context=" + CONTEXT, *args], capture=capture, check=check)


def require_cluster(home):
    for tool in ("docker", "kubectl", "terraform"):
        if not shutil.which(tool):
            raise ValueError("Instale " + tool + " antes do deploy local.")
    if not (home / "kubeconfig").is_file():
        raise ValueError("Crie primeiro o cluster pelo repositorio oficina-mecanica-infra-local.")
    config = json.loads(kube(home, "config", "view", "--minify", "-o", "json", capture=True).stdout)
    server = config["clusters"][0]["cluster"]["server"]
    if urlparse(server).hostname not in ("127.0.0.1", "localhost", "::1"):
        raise ValueError("O deploy aceita somente o cluster kind local, nao endpoints externos.")
    nodes = json.loads(kube(home, "get", "nodes", "-o", "json", capture=True).stdout)
    if not nodes["items"] or any(not any(condition["type"] == "Ready" and condition["status"] == "True"
                                         for condition in node["status"]["conditions"]) for node in nodes["items"]):
        raise ValueError("O cluster oficina-fase4 precisa estar Ready.")
    run(["docker", "info", "--format", "{{.ServerVersion}}"], capture=True)


def resources_exist(home, kind, namespace):
    result = kube(home, "get", kind, "-n", namespace, "-o", "json", capture=True, check=False)
    if result.returncode:
        if "NotFound" in result.stderr:
            return False
        raise ValueError("Nao foi possivel conferir os recursos existentes em " + namespace + ".")
    return bool(json.loads(result.stdout)["items"])


def private_json(path, create=None, existing_resources=False):
    if path.is_symlink():
        raise ValueError("O arquivo privado nao pode ser um link simbolico.")
    if not path.exists():
        if create is None or existing_resources:
            raise ValueError("Restaure o arquivo privado " + str(path) + "; nao gere novas senhas para volumes existentes.")
        path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        path.parent.chmod(0o700)
        data = create()
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(descriptor, "w") as stream:
            json.dump(data, stream, indent=2)
            stream.write("\n")
    path.chmod(0o600)
    with path.open() as stream:
        return json.load(stream)


def write_private_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix=".config-")
    try:
        with os.fdopen(descriptor, "w") as stream:
            json.dump(data, stream, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
        path.chmod(0o600)
    finally:
        Path(temporary).unlink(missing_ok=True)


def common_secrets(home, create=False):
    existing = create and any(resources_exist(home, "services", "oficina-" + service)
                              for service in ("ordens", "execucao", "cobranca"))
    return private_json(home / "secrets" / "common.json",
                        (lambda: {"jwt_secret": secrets.token_hex(32), "admin_password": secrets.token_hex(16),
                                  "ordens_cobranca_secret": secrets.token_hex(32)}) if create else None,
                        existing_resources=existing)


def ensure_kind(home):
    destination = home / "bin" / "kind"
    if destination.exists():
        version = run([destination, "version"], capture=True).stdout
        if "kind " + KIND_VERSION not in version:
            raise ValueError("O kind local deve estar fixado em " + KIND_VERSION + ".")
        return destination
    binary = KIND_BINARIES.get((platform.system(), platform.machine()))
    if binary is None:
        raise ValueError("Instale kind " + KIND_VERSION + " e configure o binario em " + str(destination) + ".")
    destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    artifact, expected = binary
    with urlopen("https://kind.sigs.k8s.io/dl/" + KIND_VERSION + "/kind-" + artifact, timeout=90) as response:
        content = response.read()
    if hashlib.sha256(content).hexdigest() != expected:
        raise ValueError("Checksum do kind diferente do publicado pelo projeto oficial.")
    descriptor, temporary = tempfile.mkstemp(dir=destination.parent, prefix=".kind-")
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
        Path(temporary).chmod(0o700)
        os.replace(temporary, destination)
    finally:
        Path(temporary).unlink(missing_ok=True)
    return destination


def terraform_apply(root, module, home, owner, values, plan_only=False):
    directory = home / "terraform"
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    directory.chmod(0o700)
    variables = home / "config" / (owner + ".tfvars.json")
    write_private_json(variables, {"runtime_dir": str(home), **values})
    environment = dict(os.environ, TF_DATA_DIR=str(root / module / ".terraform"), TF_WORKSPACE="default")
    prefix = ["terraform", "-chdir=" + str(root / module)]
    run([*prefix, "init", "-input=false", "-lockfile=readonly",
         "-backend-config=path=" + str(directory / (owner + ".tfstate"))], env=environment)
    descriptor, temporary = tempfile.mkstemp(dir=directory, prefix=owner + "-", suffix=".tfplan")
    os.close(descriptor)
    plan = Path(temporary)
    try:
        result = run([*prefix, "plan", "-json", "-input=false", "-lock-timeout=120s",
                      "-var-file=" + str(variables), "-out=" + str(plan)],
                     env=environment, capture=True, check=False)
        for line in result.stdout.splitlines():
            event = json.loads(line)
            if event.get("type") in ("planned_change", "change_summary"):
                print(event.get("@message", ""), flush=True)
            elif event.get("type") == "diagnostic":
                diagnostic = event["diagnostic"]
                print(diagnostic.get("summary", "") + "\n" + diagnostic.get("detail", ""), flush=True)
        if result.returncode:
            if result.stderr:
                print(result.stderr, flush=True)
            raise ValueError("O plano Terraform falhou; nenhum apply foi executado.")
        if not plan_only:
            run([*prefix, "apply", "-input=false", "-lock-timeout=120s", plan], env=environment)
    finally:
        plan.unlink(missing_ok=True)
        for state in directory.glob("*.tfstate*"):
            if state.is_file():
                state.chmod(0o600)
