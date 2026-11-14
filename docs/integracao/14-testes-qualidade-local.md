# BDD, cobertura e verificacao local

Esta etapa nao precisa de conta externa. Os guias 01-12 registram a evolucao
incremental; os guias 13-14 atualizam as pendencias de recuperacao, BDD e cobertura
que aparecem nos incrementos anteriores.

## BDD com os tres servicos

O runner e o Behave, com cenarios Gherkin em portugues em
tests/acceptance/features/ordem_servico.feature. Os passos chamam as APIs reais;
eventos passam pelo ElasticMQ e os servicos usam seus bancos separados.

Na raiz de Ordens, com os repositorios lado a lado e Docker aberto:

```bash
docker compose -f compose.integracao.yml up -d --build
python3 -m venv .venv-tests
.venv-tests/bin/python -m pip install -r tests/acceptance/requirements.txt
.venv-tests/bin/python -m behave tests/acceptance/features
```

O Compose usa pagamento approved e estorno refunded por padrao. Os testes criam
dados ficticios novos, nao limpam volumes e recusam URLs fora do computador local.
Portas: Ordens 18091, Execucao 18092 e Cobranca 18093. Credenciais locais podem
ser alteradas por SAGA_ADMIN_PASSWORD. Nao use essa suite em producao.

Tres cenarios verificam:

1. Criar OS, diagnosticar, cliente aprovar, pagar, reservar/consumir pecas,
   finalizar, entregar e consultar como cliente, com entrega idempotente.
2. Cliente recusar: cancelar OS/Execucao sem pagamento ou consumo de pecas.
3. Cancelar reparo pago: parar Execucao, liberar reservas, estornar e cancelar
   OS, preservando a cobranca original e o estoque fisico.

## Cobertura minima por servico

Cada repositorio tem cobertura.runsettings e tools/verificar_cobertura.py.
Coleta Cobertura/OpenCover; inclui Domain, Application, InterfaceAdapters,
Infrastructure e Api. Exclui somente migrations EF geradas automaticamente.
O executavel do provedor MOCK e uma ferramenta de teste separada da API de Cobranca.

Na raiz de cada servico, usando uma pasta de resultado NOVA por execucao:

```bash
dotnet test --configuration Release --settings cobertura.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/cobertura-atual
python3 tools/verificar_cobertura.py --servico Ordens --diretorio TestResults/cobertura-atual --json TestResults/cobertura-resumo.json
```

Troque Ordens por Execucao ou Cobranca no respectivo repositorio. O script une
hits por projeto/arquivo/linha entre unidade e integracao; nao soma percentuais
nem duplica o denominador. Falha abaixo de 80%, se faltar relatorio ou qualquer
uma das cinco camadas. Nao reutilize uma pasta com resultados antigos, pois
cobertura antiga nao representa o codigo atual. BDD HTTP nao entra nesta conta.

Para nao ignorar testes de transporte/persistencia, use os bancos do Compose:

```bash
export SQS_TEST_ENDPOINT=http://127.0.0.1:9324
```

Em Execucao, configure tambem:

```bash
export MONGODB_TEST_CONNECTION_STRING='mongodb://execucao_root:LocalMongoSaga123@127.0.0.1:27019/?authSource=admin&replicaSet=rs0&directConnection=true'
```

Em Cobranca, configure tambem (os testes criam e excluem apenas seus bancos temporarios):

```bash
export MYSQL_TEST_CONNECTION_STRING='Server=127.0.0.1;Port=3310;User=root;Password=LocalRootCobrancaSaga123;AllowPublicKeyRetrieval=true;SslMode=None'
```

Nao use conexoes de producao ou banco compartilhado nos testes.

Na raiz de Ordens, testes das ferramentas locais, incluindo replay isolado:

```bash
SQS_TEST_ENDPOINT=http://127.0.0.1:9324 .venv-tests/bin/python -m unittest discover -s tests/local -v
```

## Evidencia local deste incremento

Medicao de linhas unidas, com as cinco camadas incluidas e somente migrations
geradas excluidas:

| Servico | Unitarios | Integracao | Linhas cobertas/total | Cobertura |
| --- | ---: | ---: | ---: | ---: |
| Ordens | 98 | 157 | 2461/2617 | 94,04% |
| Execucao | 54 | 61 | 1990/2066 | 96,32% |
| Cobranca | 55 | 114 | 1696/1837 | 92,32% |

Total: 539 testes .NET, sem ignorados nesta verificacao. Tambem passaram 10
testes das ferramentas locais, os 3 cenarios BDD e o script HTTP de recuperacao
financeira com resposta perdida no Docker. Esses numeros sao uma fotografia
local, nao um resultado de Sonar ou de deploy em nuvem. Reexecute apos editar.

## Pendencias externas deliberadas

Integrar BDD/verificador ao CI, configurar SonarQube/SonarQube Cloud, cadastrar
Secrets/Variables e habilitar quality gate ficam para a etapa de pipelines.
Nao foram criadas contas, tokens, projetos Sonar ou recursos AWS. A cobertura
ja pode ser comprovada localmente; isso nao equivale a analise Sonar pronta.
