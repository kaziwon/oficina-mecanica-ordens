# Aprovacao e recusa do orcamento na Saga

## Escopo e fluxo

As APIs administrativas de Cobranca continuam iguais. Para um orcamento criado
pela Saga, agora elas tambem notificam Ordens. Um orcamento manual, sem Saga,
continua independente. O [incremento de pagamento](05-pagamento-mock.md) agora
complementa este fluxo quando habilitado; a liberacao do reparo continua pendente.

```text
Aprovar no Billing -> MySQL: resposta + historico + evento na outbox
  -> ordens-eventos -> Ordens: Saga e OS AguardandoPagamento
  -> Execucao permanece AguardandoLiberacao

Recusar no Billing -> MySQL: resposta + historico + evento na outbox
  -> ordens-eventos -> Ordens: CancelamentoPorRecusaPendente + comando na outbox
  -> execucao-comandos -> Execucao: Cancelada + resposta no mesmo documento Mongo
  -> ordens-eventos -> Ordens: Saga OrcamentoRecusado e OS Cancelada
```

Aprovacao nao e pagamento. O novo status operacional AguardandoPagamento tem
valor 8, sem renumerar estados anteriores. A listagem prioriza EmExecucao,
AguardandoPagamento, AguardandoAprovacao, EmDiagnostico e Recebida. Canceladas
ficam fora da fila operacional, mas continuam acessiveis por ID com historico.

## Mensagens v1

O envelope e o mesmo dos incrementos anteriores. `correlationId` e `sagaId`
permanecem os originais, mesmo se a requisicao HTTP de aprovacao usar outro ID.

| Mensagem | Origem -> destino | CausationId |
|---|---|---|
| OrcamentoAprovado | Cobranca -> Ordens | Pedido GerarOrcamento |
| OrcamentoRecusado | Cobranca -> Ordens | Pedido GerarOrcamento |
| CancelarExecucaoPorRecusaOrcamento | Ordens -> Execucao | Evento OrcamentoRecusado |
| ExecucaoCanceladaPorRecusaOrcamento | Execucao -> Ordens | Comando de cancelamento por recusa |
| CancelamentoPorRecusaOrcamentoRecusado | Execucao -> Ordens | Comando de cancelamento por recusa |

Todos identificam ordemServicoId, execucaoId e orcamentoId no payload. A decisao
inclui valorTotal e orcamentoCriadoEm UTC. A recusa e seu comando exigem motivo.
A resposta negativa de Execucao inclui codigo e motivo; a positiva nao.

O evento de decisao usa o instante da resposta no occurredAt. Como a fila nao
garante ordenacao, esse evento tambem comprova a criacao do orcamento. Ordens
consegue aplica-lo antes de OrcamentoGerado, reconstruindo o historico com
orcamentoCriadoEm. A confirmacao atrasada nao faz a Saga voltar de estado.

## Consistencia e compensacao

- Cobranca grava resposta, historico e evento na mesma transacao. Se a outbox
  falhar, a resposta tambem e revertida. Uma disputa de versao nao publica o
  evento da resposta perdedora. Repeticoes nao geram novos eventos.
- Aprovar depois de recusar ou recusar depois de aprovar retorna 409. Repetir
  a mesma recusa exige o mesmo motivo, conforme a regra existente.
- Ordens valida causa, correlacao, IDs, valor e contrato. Saga, inbox, historico
  e comando de compensacao sao gravados em uma unica transacao local.
- Na recusa, a OS so fica Cancelada depois da confirmacao de Execucao. Antes
  disso, a Saga permanece CancelamentoPorRecusaPendente.
- Execucao so cancela uma entrada diagnosticada em AguardandoLiberacao.
  Cancelamento e resposta ficam no mesmo documento Mongo, com controle de
  versao. Reentregas nao duplicam historico ou resposta.
- Reparo ja liberado/iniciado produz a recusa ExecucaoNaoCancelavel. Ordens
  registra FalhaRequerIntervencao e nao inventa um cancelamento da OS.
- Dados de outro processo nao alteram a execucao nem geram confirmacoes falsas.
  Falhas tecnicas e mensagens malformadas seguem retentativas/DLQ como antes.

A compensacao retira o trabalho nao autorizado da fila sem apagar diagnostico,
orcamento, itens ou historico. Ainda nao ha reserva de estoque nem pagamento
confirmado neste ponto: nao existe estoque para devolver nem estorno a simular.
Essas etapas terao compensacoes proprias quando forem implementadas.

## Teste local

Com os tres repositorios em versoes compativeis e Docker Desktop aberto,
execute na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-resposta-orcamento.py
```

O script utiliza somente HTTP. Gera dois orcamentos a partir de diagnosticos,
testa aprovacao, recusa, repeticoes e resposta contraditoria. Confere que a
aprovacao nao libera reparos e que a recusa cancela as duas
filas sem apagar o diagnostico. Cria apenas dados ficticios locais.

Os roteiros anteriores tambem continuam disponiveis:

```bash
python3 infra/local/testar-abertura.py
python3 infra/local/testar-diagnostico-orcamento.py
```

### Postman

1. Gere um orcamento seguindo o [guia anterior](03-diagnostico-orcamento.md).
2. Faca login em Cobranca na porta 18093 e use o JWT dessa API.
3. Aprove com POST /api/orcamentos/ID_DO_ORCAMENTO/aprovar, sem body.
4. Em Ordens, consulte GET /api/ordensservico/ID_DA_OS/saga-abertura. Aguarde
   AguardandoPagamento, com orcamentoAprovado=true e data da resposta.
5. Em outra OS, use POST /api/orcamentos/ID_DO_ORCAMENTO/recusar:

```json
{ "motivo": "Cliente nao autorizou o valor" }
```

6. A Saga passa por CancelamentoPorRecusaPendente e chega a OrcamentoRecusado.
   Consulte OS e Execucao por ID: ficam Cancelada, com os registros preservados.

HTTP 200 de Cobranca confirma somente a transacao local; aguarde o processamento
assincrono antes de conferir os outros servicos.

## Atualizacao e limites

As migrations sao aplicadas automaticamente no Compose local. A de Cobranca
adiciona a correlacao ao orcamento e a recupera do evento OrcamentoGerado na
outbox para registros integrados ja existentes. O CI testa esse caminho com
MySQL real. Um registro sem contexto suficiente nao e respondido como se
pudesse notificar uma Saga desconhecida.

Respostas anteriores a esta versao nao sao publicadas retroativamente: nao ha
reprocessamento automatico de aprovacoes/recusas historicas. O pagamento mock
esta descrito no guia 05; depois vem a liberacao de Execucao. Os testes HTTP deste
incremento nao substituem o BDD completo, a cobertura minima e o Sonar previstos
para a entrega. Nada aqui provisiona recursos AWS.
