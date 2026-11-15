#!/usr/bin/env python3
"""Build e deploy independente do servico no kind local da Fase 4."""

import argparse
import datetime
import json
import os
import re
import secrets
import subprocess
import sys
from pathlib import Path

from cd_gate import require_approved_revision
from deploy_settings import IMAGE_BUILDS, new_private_secrets, service_values
from new_relic_config import read_new_relic
from local_runtime import (CLUSTER, common_secrets, ensure_kind, kube, operation_lock,
                           private_json, require_cluster, resources_exist, run, runtime_home, terraform_apply)


def deploy(root, args):
    config = json.loads((root / "k8s" / "deployment.json").read_text())
    service = config["service"]
    if service not in ("ordens", "execucao", "cobranca"):
        raise ValueError("Servico desconhecido.")
    solutions = list(root.glob("OficinaMecanica.*.slnx"))
    if (len(solutions) != 1 or solutions[0].name != config["solution"]
            or config["solution"] != "OficinaMecanica." + service.title() + ".slnx"):
        raise ValueError("A configuracao de deploy nao pertence ao servico deste repositorio.")
    home = runtime_home()
    if args.ci:
        head = run(["git", "-C", root, "rev-parse", "HEAD"], capture=True).stdout.strip()
        if head != args.sha or run(["git", "-C", root, "status", "--porcelain"], capture=True).stdout:
            raise ValueError("O checkout precisa estar limpo e corresponder ao SHA aprovado.")
    tag = args.sha if args.ci else (args.image_tag or "dev-" + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dt%H%M%S") + "-" + secrets.token_hex(3))
    if not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", tag):
        raise ValueError("Tag de imagem invalida.")
    require_cluster(home)
    with operation_lock(home):
        if args.ci:
            require_approved_revision(args.sha, args.repository)
        common = common_secrets(home)
        private = private_json(home / "secrets" / (service + ".json"), new_private_secrets,
            existing_resources=resources_exist(home, "pvc", "oficina-" + service))
        kube(home, "rollout", "status", "statefulset/elasticmq", "-n", "oficina-plataforma", "--timeout=180s")
        values = service_values(common, private)
        observability = read_new_relic(home)
        values["new_relic_enabled"] = observability["enabled"]
        values["api_secrets"]["NEW_RELIC_LICENSE_KEY"] = observability["license_key"]
        images = []
        for variable, (name, dockerfile) in IMAGE_BUILDS.items():
            values[variable] = name + ":" + tag
            images.append((values[variable], dockerfile))
        if not args.plan_only:
            kind = ensure_kind(home)
            for image_name, dockerfile in images:
                run(["docker", "build", "--tag", image_name, "--file", root / dockerfile, root])
                run([kind, "load", "docker-image", "--name", CLUSTER, image_name])
        terraform_apply(root, Path("infra/kubernetes"), home, service, values, args.plan_only)
        if not args.plan_only:
            kube(home, "rollout", "status", "deployment/api", "-n", "oficina-" + service, "--timeout=300s")
            print("Deploy concluido: http://127.0.0.1:" + str(config["host_port"]) + "/swagger/index.html")


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan-only", action="store_true")
    parser.add_argument("--image-tag")
    parser.add_argument("--ci", action="store_true")
    parser.add_argument("--sha", default="")
    parser.add_argument("--repository", default="")
    args = parser.parse_args()
    try:
        deploy(Path(__file__).resolve().parents[1], args)
    except (ValueError, TimeoutError, OSError, subprocess.CalledProcessError) as error:
        print("Deploy local interrompido: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
