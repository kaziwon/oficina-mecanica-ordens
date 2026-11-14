#!/usr/bin/env bash
set -euo pipefail
set +x

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"
SOMENTE_TESTES=false
case "${1:-}" in
  '') ;;
  --somente-testes) SOMENTE_TESTES=true ;;
  *) printf 'Uso: bash scripts/sonar_scan.sh [--somente-testes]\n' >&2; exit 2 ;;
esac
if [ "$#" -gt 1 ]; then exit 2; fi

SOLUCOES=("$ROOT_DIR"/OficinaMecanica.*.slnx)
if [ "${#SOLUCOES[@]}" -ne 1 ]; then printf 'Solucao do servico nao identificada.\n' >&2; exit 1; fi
case "$(basename "${SOLUCOES[0]}")" in
  OficinaMecanica.Ordens.slnx) SERVICO=Ordens; CHAVE=oficina-mecanica-ordens ;;
  OficinaMecanica.Execucao.slnx) SERVICO=Execucao; CHAVE=oficina-mecanica-execucao ;;
  OficinaMecanica.Cobranca.slnx) SERVICO=Cobranca; CHAVE=oficina-mecanica-cobranca ;;
  *) printf 'Servico desconhecido.\n' >&2; exit 1 ;;
esac
for ferramenta in docker dotnet python3 curl; do
  command -v "$ferramenta" >/dev/null || { printf 'Instale %s antes de continuar.\n' "$ferramenta" >&2; exit 1; }
done

if [ "$SOMENTE_TESTES" = false ]; then
  if [ -z "${SONAR_TOKEN:-}" ] && [ -t 0 ]; then
    read -r -s -p 'Token de analise deste projeto no Sonar local: ' SONAR_TOKEN
    printf '\\n'
  fi
  : "${SONAR_TOKEN:?Defina SONAR_TOKEN com um token do projeto local; nao envie o token no chat.}"
  SONAR_HOST_URL="${SONAR_HOST_URL:-http://127.0.0.1:9000}"
  export SONAR_HOST_URL
  python3 -c 'import os; from urllib.parse import urlparse; u=urlparse(os.environ["SONAR_HOST_URL"]); assert u.scheme=="http" and u.hostname in ("localhost","127.0.0.1","::1") and not(u.username or u.password or u.query or u.fragment), "Esta configuracao aceita somente SonarQube local."'
  curl --fail --silent --show-error --max-time 10 "${SONAR_HOST_URL%/}/api/system/status" \
    | python3 -c 'import json,sys; assert json.load(sys.stdin)["status"]=="UP", "SonarQube ainda nao esta pronto."'
fi

docker compose version >/dev/null
PROJETO="sonar-testes-$(printf '%s' "$SERVICO" | tr '[:upper:]' '[:lower:]')-$$-${RANDOM}"
COMPOSE=(docker compose --project-name "$PROJETO" --file "$ROOT_DIR/compose.qualidade.yml")
RESULTADOS="$ROOT_DIR/TestResults/sonar/$(date -u +%Y%m%dT%H%M%SZ)-$$"
mkdir -p "$RESULTADOS"
limpar() {
  local status=$?
  trap - EXIT
  "${COMPOSE[@]}" down --volumes --remove-orphans >/dev/null 2>&1 \
    || printf 'Nao foi possivel limpar os containers de teste do projeto %s.\n' "$PROJETO" >&2
  exit "$status"
}
trap limpar EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

"${COMPOSE[@]}" up --detach --wait --wait-timeout 180
porta() {
  local endereco
  endereco="$("${COMPOSE[@]}" port "$1" "$2")"
  endereco="${endereco##*:}"
  case "$endereco" in ''|*[!0-9]*) printf 'Porta de teste invalida.\n' >&2; return 1 ;; esac
  printf '%s' "$endereco"
}
export SQS_TEST_ENDPOINT="http://127.0.0.1:$(porta elasticmq 9324)"
case "$SERVICO" in
  Execucao)
    "${COMPOSE[@]}" exec --no-TTY mongo mongosh --quiet --file /dev/stdin < infra/local/mongo-replica-init.js
    export MONGODB_TEST_CONNECTION_STRING="mongodb://127.0.0.1:$(porta mongo 27017)/?replicaSet=rs0&directConnection=true"
    ;;
  Cobranca)
    export MYSQL_TEST_CONNECTION_STRING="Server=127.0.0.1;Port=$(porta mysql 3306);User=root;Password=LocalCiSonarCobranca123;AllowPublicKeyRetrieval=true;SslMode=None"
    ;;
esac

if [ "$SOMENTE_TESTES" = false ]; then
  dotnet tool restore
  dotnet sonarscanner begin \
    /k:"$CHAVE" /n:"Oficina Mecanica - $SERVICO" \
    /d:sonar.host.url="${SONAR_HOST_URL%/}" /d:sonar.token="$SONAR_TOKEN" \
    /d:sonar.scanner.scanAll=false \
    /d:sonar.exclusions="**/Persistence/Migrations/**,tools/OficinaMecanica.PagamentoMock/**" \
    /d:sonar.cs.opencover.reportsPaths="$RESULTADOS/**/coverage.opencover.xml" \
    /d:sonar.qualitygate.wait=true /d:sonar.qualitygate.timeout=300
fi

dotnet restore "${SOLUCOES[0]}"
dotnet build "${SOLUCOES[0]}" --configuration Release --no-restore --no-incremental --disable-build-servers
dotnet test "${SOLUCOES[0]}" --configuration Release --no-build --no-restore \
  --settings cobertura.runsettings --collect:"XPlat Code Coverage" \
  --logger "trx;LogFilePrefix=qualidade" --results-directory "$RESULTADOS"
python3 tools/verificar_testes.py --diretorio "$RESULTADOS"
status_cobertura=0
python3 tools/verificar_cobertura.py --servico "$SERVICO" --diretorio "$RESULTADOS" \
  --json "$RESULTADOS/cobertura-resumo.json" || status_cobertura=$?

if [ "$SOMENTE_TESTES" = false ]; then
  dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
else
  printf 'Somente testes: nenhuma analise Sonar foi enviada.\n'
fi
printf 'Relatorios locais: %s\n' "$RESULTADOS"
exit "$status_cobertura"
