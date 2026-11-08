# Primeiro fluxo integrado - executar e entender

## O que funciona agora

```text
POST OS -> MySQL: OS + Saga + outbox
                    |
              worker de Ordens -> SQS -> worker de Execucao
                                             |
                         MongoDB: entrada + resposta pendente
                                             |
              worker de Ordens <- SQS <- publicador de Execucao
                    |
             MySQL: Saga + inbox
```

As transacoes sao locais. A Saga coordena o processo; nao existe uma transacao
que abra os bancos dos dois servicos. A outbox guarda o envio pendente. A inbox
e a identificacao dos comandos ja processados impedem repetir os efeitos.

O transporte usa o SDK oficial SQS, restrito a Infrastructure. Para desenvolver
sem AWS, ElasticMQ oferece uma API compativel. `Messaging:UsarAws` permanece
falso neste ambiente; nao e preciso iniciar Lab ou cadastrar credenciais AWS.

Uma resposta so e removida da fila depois da persistencia. Se o processo cair
entre enviar e marcar como publicado, pode haver outra entrega; a identificacao
e a versao impedem duplicar a entrada ou o historico.

## Subir os dois servicos

Mantenha os repositorios lado a lado, nas branches/commits compativeis:

```text
Fiap/
  oficina-mecanica-ordens/
  oficina-mecanica-execucao/
```

Com Docker Desktop aberto, execute na raiz de **Ordens**:

```bash
docker compose -f compose.integracao.yml up --build -d
```

O comando cria duas APIs, MySQL, MongoDB e ElasticMQ. O pequeno servico
`filas-permissoes` prepara o volume e encerra; o simulador roda sem root.
Os volumes desse Compose sao separados dos volumes dos projetos individuais.
As credenciais explicitas no arquivo sao somente de desenvolvimento.

Se Execucao estiver em outro lugar, informe seu caminho antes do comando:

```bash
export EXECUCAO_PATH=/caminho/para/oficina-mecanica-execucao
```

| Componente | Endereco local |
|---|---|
| Swagger de Ordens | http://localhost:18091/swagger/index.html |
| Swagger de Execucao | http://localhost:18092/swagger/index.html |
| SQS simulado | http://localhost:9324 |
| MySQL de Ordens | 127.0.0.1:3309 |
| MongoDB de Execucao | 127.0.0.1:27019 |

As filas sao `execucao-comandos` e `ordens-eventos`, cada uma com sua DLQ. Apos
cinco recebimentos sem confirmacao, uma mensagem vai para a respectiva DLQ.
Mensagens malformadas nao alteram uma OS e nao produzem uma recusa de negocio
inventada. Precisam de investigacao/reprocessamento, assim como falhas tecnicas
que excederem esse numero de tentativas.

ElasticMQ persiste mensagens em seu volume para permitir testar reinicio local.
Ele e um simulador, nao uma garantia de equivalencia completa com o servico AWS.
O provisionamento e as permissoes das filas reais ficam para o deploy futuro.

## Testar pelo Postman

1. Faca login em Ordens: `POST http://localhost:18091/api/auth/login`.

```json
{ "username": "admin", "password": "LocalAdminSaga123" }
```

2. Use o JWT como Bearer e crie uma OS pelo body ja documentado no README.
   HTTP 201 confirma a gravacao local, nao que Execucao ja consumiu o comando.
3. Consulte `GET /api/ordensservico/ID_DA_OS/saga-abertura` em Ordens.
   O status vai de `AguardandoRegistro` para `RegistroConfirmado`, com `execucaoId`.
4. Faca login em Execucao pela porta 18092, com as mesmas credenciais locais, e
   consulte `GET /api/execucoes/ID_DA_EXECUCAO`. Ela estara `AguardandoDiagnostico`.

O status operacional da OS continua `Recebida`. Este trecho nao significa que
o diagnostico ja comecou e ainda nao sincroniza suas transicoes posteriores.

Para compensar essa entrada, em Ordens:

```text
POST /api/ordensservico/ID_DA_OS/cancelar-entrada
```

```json
{ "motivo": "Cliente desistiu antes do diagnostico" }
```

HTTP 202 indica que o pedido foi persistido. A Saga fica `CompensacaoPendente`
ate Execucao responder. Depois, `Compensada` confirma o cancelamento nos dois
servicos, sem apagar cliente, veiculo ou historico. Repetir o pedido nao cria
um segundo comando.

Se ja iniciou o diagnostico, o resultado sera `FalhaRequerIntervencao`; o
cancelamento automatico nao desfaz trabalho em andamento. Este endpoint nao e
um cancelamento geral de reparos, orcamentos ou pagamentos.

Uma entrada manual com dados divergentes tambem exige intervencao: a OS nao e
cancelada automaticamente, pois pode existir trabalho em outro processo. Somente
a recusa `ProcessoCancelado` confirma uma entrada ja cancelada e permite refletir
esse cancelamento em Ordens. Codigos desconhecidos nao autorizam desfazer a OS.

## Teste automatizado ponta a ponta

Depois de subir o Compose, na raiz de Ordens:

```bash
python3 infra/local/testar-abertura.py
```

O script usa apenas HTTP e espera os healthchecks. Cria dados exclusivos de
teste e verifica entrada automatica, compensacao, repeticao sem novo historico
e recusa de compensacao depois de iniciar diagnostico. Nao apaga esses dados.
As variaveis `ORDENS_URL`, `EXECUCAO_URL` e `SAGA_ADMIN_PASSWORD` permitem mudar
os enderecos e a credencial de desenvolvimento.

Este e um teste de integracao do primeiro trecho. O BDD do fluxo completo dos
tres servicos continua sendo um proximo incremento.

## Simular falha e recuperacao

Pare a fila, mantendo os bancos e as APIs:

```bash
docker compose -f compose.integracao.yml stop elasticmq
```

Abra outra OS pelo Postman: ela sera gravada e ficara `AguardandoRegistro`.
Reinicie Ordens para testar que o pedido nao depende da memoria do processo:

```bash
docker compose -f compose.integracao.yml restart ordens
docker compose -f compose.integracao.yml start elasticmq
```

Consulte a Saga novamente: o worker recupera a outbox e chega a
`RegistroConfirmado`, sem recriar a OS. Isso nao e rollback por falha de rede;
e uma nova tentativa da operacao ja persistida.

Para ver os logs JSON com SagaId, MessageId e CorrelationId:

```bash
docker compose -f compose.integracao.yml logs -f ordens execucao
```

O rastreamento APM entre os processos sera conectado na etapa de observabilidade.

## Encerrar

```bash
docker compose -f compose.integracao.yml down
```

Esse comando mantem os volumes. Remover volumes apaga os dados locais desses
testes; nao e necessario para encerrar o ambiente.
