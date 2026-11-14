# Cancelamento de reparo pago e estorno mock

## Ordem da Saga

Depois de pagar, o cancelamento precisa desfazer os efeitos em ordem. Ordens
orquestra, mas cada servico grava somente em seu proprio banco:

```text
Admin -> POST /api/ordensservico/{id}/cancelar-reparo
Ordens: CancelamentoPagoPendente -> CancelarReparoPago -> Execucao
Execucao: para reparo + libera reservas + resposta duravel, transacao Mongo
ReparoPagoCancelado -> Ordens: OS CancelamentoPendente + SolicitarEstorno
Cobranca: estorno Pendente -> token OAuth -> POST /refunds no mock
EstornoConfirmado -> Ordens: Saga CompensadaAposPagamento + OS Cancelada
```

O POST exige JWT Admin de Ordens e body `{"motivo":"Desistencia do cliente"}`.
Retorna 202 com a Saga; 404 para OS inexistente, 400 para motivo invalido e 409
para reparo nao elegivel. Repetir com o mesmo motivo devolve a operacao existente,
sem outro comando; mudar o motivo de uma operacao existente retorna 409.
Consulte GET /api/ordensservico/{id}/saga-abertura para acompanhar o resultado.

Este fluxo aceita pagamento confirmado com liberacao pendente, fila liberada
ou reparo em andamento. Inclui uma falha conhecida da liberacao apos pagamento.
Nao tenta estornar uma cobranca cujo resultado ainda e desconhecido. Os fluxos
anteriores de cancelamento de entrada e recusa de orcamento continuam separados.

## Estoque e disputa com conclusao

Cancelar reserva so diminui quantidadeReservada: nao soma ao estoque fisico,
pois nenhuma peca foi consumida ainda. As reservas recebem liberadaEm, mantendo
consumidaEm vazio. Antes do inicio nao existem reservas a liberar.

Uma unica transacao Mongo salva execucao Cancelada, historico, resposta outbox
e liberacao de todas as reservas. Se uma peca falha, tudo e desfeito. Replica
set e obrigatorio, como ja era para iniciar e concluir o reparo.

Conclusao e cancelamento usam a mesma versao da execucao. Apenas um vence.
Se o reparo ja concluiu, Execucao responde CancelamentoReparoPagoRecusado:
Ordens restaura o acompanhamento anterior e NAO solicita estorno. A conclusao
que chegou enquanto o cancelamento estava pendente pode ser reentregue.
Uma OS ja Finalizada ou Entregue rejeita o pedido HTTP de cancelamento.
Pecas consumidas nao sao devolvidas ficticiamente ao estoque.

## Historico financeiro

Cobranca grava um Estorno separado, vinculado ao Pagamento confirmado original.
O pagamento permanece Confirmado como registro da cobranca realizada; a situacao
do reembolso esta em GET /api/pagamentos/{pagamentoId}/estorno, com JWT Admin.
O retorno mostra status, valor, tentativas, datas e identificador do provedor.
Ausencia de estorno retorna 404. Nao existe endpoint publico para forcar sucesso.

O ID do comando SolicitarEstorno e tambem o EstornoId e X-Idempotency-Key enviado
ao mock. Repeticoes usam esse mesmo ID. Valor e BRL vem do pagamento persistido,
nao de um body HTTP. O mock confere a cobranca original e aceita uma unica
operacao de estorno integral por pagamento. Nada movimenta dinheiro real.

Se o mock responde, mas a gravacao local falha, a pendencia continua no banco.
A proxima tentativa usa a mesma chave e recebe o mesmo resultado. Se a gravacao
conclui, estorno e evento outbox ficam na mesma transacao MySQL. Duas versoes
concorrentes nao gravam dois resultados. Os comandos possuem inbox e validacao
dos IDs de Saga, OS, execucao, orcamento, pagamento, provedor, valor e correlacao.

## Recusa ou indisponibilidade

EstornoRecusado e uma recusa conhecida. Timeout, HTTP inesperado ou resposta
divergente sao falha tecnica: nao significam que o dinheiro foi devolvido.
O worker tenta novamente com a mesma chave ate PagamentoMock:MaxTentativas.
Ao esgotar, registra FalhaRequerIntervencao e envia FalhaProcessamentoEstorno.

Em ambos os casos Ordens fica EstornoRequerIntervencao e a OS permanece
CancelamentoPendente, com execucao parada e reservas liberadas. Nao registra
estornadoEm nem Cancelada. Nao reinicia o reparo e nao cobra novamente.
Enquanto Cobranca esta fora do ar, a Saga fica EstornoPendente: mensagens e
pendencias duraveis permitem retomar quando o servico retorna.

Reconciliacao operacional apos esgotar tentativas, nova tentativa administrativa,
reembolso parcial, cancelamento de trabalho ja concluido e replay de DLQ ainda
nao estao implementados. Nao gere outro ID nem altere status direto no banco
para contornar uma incerteza financeira. Os comandos internos pressupoe filas
confiaveis; permissoes IAM especificas ficam para a etapa AWS da Fase 4.

## Testar localmente

Na raiz de Ordens, com os tres repositorios lado a lado:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-compensacao-paga.py
```

Cria dados novos, paga e cancela depois da reserva, cancela antes do inicio e
confere recusa apos conclusao. Verifica estoque, historico, estorno separado e
repeticao sem duplicidade. Nao apaga volumes ou registros antigos.

Para testar uma recusa financeira em novos registros:

```bash
PAGAMENTO_MOCK_ESTORNO_RESULTADO=rejected docker compose -f compose.integracao.yml up -d pagamento-mock
python3 infra/local/testar-compensacao-paga.py --resultado rejected
```

Para simular provedor indisponivel e verificar que nao inventa confirmacao:

```bash
PAGAMENTO_MOCK_ESTORNO_RESULTADO=unavailable docker compose -f compose.integracao.yml up -d pagamento-mock
python3 infra/local/testar-compensacao-paga.py --resultado unavailable
```

Restaure o padrao ao terminar, mesmo se um teste falhar:

```bash
PAGAMENTO_MOCK_ESTORNO_RESULTADO=refunded docker compose -f compose.integracao.yml up -d pagamento-mock
```

O modo so afeta operacoes ainda nao persistidas. Mudar o modo nao modifica um
resultado ja conhecido. Para simular Cobranca fora do ar, pare apenas esse
servico APOS o pagamento e inicio, solicite cancelamento, observe EstornoPendente
e OS CancelamentoPendente; reinicie Cobranca e acompanhe a confirmacao.
Nunca pare ou destrua o banco para esse teste. Nenhum comando cria recursos AWS.

Os testes automatizados cobrem tambem rollback entre pecas, concorrencia com
conclusao, causa/valor divergentes, eventos atrasados, idempotencia apos reinicio
do mock e recuperacao apos falha de gravacao financeira.
