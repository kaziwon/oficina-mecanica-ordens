"""Exige CI e Sonar aprovados para o SHA exato da main antes do CD."""

import json
import os
import re
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def validate_runs(runs, sha, repository):
    candidates = [run for run in runs if run["head_sha"] == sha and run["head_branch"] == "main"
                  and (run.get("head_repository") or {}).get("full_name") == repository]
    if not candidates:
        raise ValueError("Nao existe verificacao aprovada para o SHA exato da main.")
    latest = max(candidates, key=lambda run: (run["run_number"], run.get("run_attempt", 1)))
    if latest["status"] != "completed" or latest["conclusion"] != "success":
        raise ValueError("A verificacao mais recente desta revisao ainda nao foi aprovada.")
    if latest["event"] not in ("push", "workflow_run", "workflow_dispatch"):
        raise ValueError("Execucoes de PR/fork nao autorizam deploy local.")


def require_approved_revision(sha, repository):
    if not re.fullmatch(r"[0-9a-f]{40}", sha) or not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Informe o repositorio e SHA completos do GitHub.")
    token = os.environ.get("GITHUB_TOKEN", "")
    if not token:
        raise ValueError("GITHUB_TOKEN nao foi fornecido pelo workflow.")

    def get(route):
        request = Request("https://api.github.com/repos/" + repository + route,
                          headers={"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                                   "X-GitHub-Api-Version": "2022-11-28"})
        with urlopen(request, timeout=30) as response:
            return json.load(response)

    if get("/commits/main")["sha"] != sha:
        raise ValueError("Esta revisao foi superada por uma main mais recente; nao sera implantada.")
    for workflow in ("integracao-continua.yml", "qualidade-local.yml"):
        query = urlencode({"branch": "main", "head_sha": sha, "per_page": 100})
        validate_runs(get("/actions/workflows/" + workflow + "/runs?" + query)["workflow_runs"], sha, repository)
