# Conclusao do reparo e consumo das reservas

## Fluxo

O administrador conclui um reparo que ja esta EmExecucao:

```text
POST /api/execucoes/{id}/concluir-reparo (JWT Admin, sem body)
  -> transacao local Mongo: consumo + reservas encerradas + Finalizada + outbox
  -> ReparoConcluido na fila ordens-eventos
  -> Ordens: inbox + Saga Finalizada + OS Finalizada + historico
```

Concluir antes de iniciar retorna 409. ID inexistente retorna 404, falta de
autenticacao retorna 401. Repetir depois do sucesso retorna 200 com o estado
persistido, sem novo consumo. Nao recebe pecas, valor ou resultado de pagamento
no body e nao faz nenhuma chamada financeira.

Finalizada significa reparo terminado, nao veiculo entregue. Nao altera o
status da OS para Entregue. O [fluxo de entrega](09-entrega-veiculo.md) esta
implementado como uma operacao administrativa separada em Ordens.

## O que acontece com o estoque

O inicio ja havia separado as pecas, sem baixa fisica. Na conclusao, cada
quantidade e subtraida de quantidadeEstoque e quantidadeReservada. Por exemplo:

| Momento | Fisico | Reservado | Disponivel |
|---|---:|---:|---:|
| Antes do inicio | 10 | 0 | 10 |
| Durante o reparo | 10 | 2 | 8 |
| Apos a conclusao | 8 | 0 | 8 |

A reserva continua no historico de Execucao, agora com consumidaEm. Nao e
apagada. reparoConcluidoEm identifica a conclusao; diagnostico e precos
aprovados continuam preservados. Reparo somente com servicos finaliza com
reservas vazias, sem movimentar pecas.

O dominio confere status, autorizacao, inicio e correspondencia entre reservas
e itens do diagnostico. A source transacional carrega cada peca e aplica a
regra de consumo do dominio. Uma peca ausente/inativa, reserva insuficiente ou
saldo fisico inconsistente impede a operacao inteira. Nao ajusta saldos
automaticamente para esconder inconsistencias.

## Transacao e repeticao

MongoConclusaoReparoDataSource usa o mesmo replica set ja preparado no
incremento anterior. A transacao confirma consumo de todas as pecas, marcas
consumidaEm, status, historico e evento pendente. Se a segunda peca falhar,
o consumo da primeira tambem e desfeito e Execucao continua EmExecucao.

Controle de versao protege requisicoes concorrentes e o driver trata conflitos
transitorios da transacao. Depois do commit, uma repeticao encontra a conclusao
salva e nao faz outra baixa. A resposta HTTP e montada do registro persistido,
preservando a mesma data nas repeticoes. Duas OS com reservas da mesma peca
consomem somente as suas quantidades.

O worker publica a outbox depois do commit e incrementa a versao ao marcar o
envio. Uma copia antiga nao desfaz essa publicacao. Nao ha transacao distribuida
com o MySQL de Ordens: a convergencia ocorre pelas mensagens duraveis da Saga.

## Mensagem v1 e ordenacao

ReparoConcluido preserva sagaId e correlationId. Assim como ReparoIniciado,
causationId identifica LiberarExecucao, a autorizacao comum aos dois fatos.
O payload identifica ordemServicoId, execucaoId, orcamentoId, pagamentoId,
provedorId, valorTotal, moeda BRL, reparoIniciadoEm e reparoConcluidoEm.
occurredAt e a data da conclusao. Todas as datas da mensagem sao UTC.

Ordens confere IDs, autorizacao, valor e a data de inicio ja registrada.
Datas divergentes, conclusao anterior ao inicio ou campos indevidos nao
finalizam a OS. Se a conclusao chegar antes do inicio/liberacao, a mensagem
nao e confirmada e pode ser reentregue depois dos eventos anteriores.

Inbox, Saga, OS e historico final sao gravados na mesma transacao MySQL.
Reentregas nao duplicam historico. Um inicio, pagamento ou confirmacao de
liberacao atrasados nao reabrem a OS finalizada. A politica de DLQ existente
continua valendo; reconciliacao de mensagens esgotadas ainda precisa de fluxo.

## Consultas e dados existentes

Finalizadas saem das listagens operacionais de Ordens e Execucao, mas o GET
por ID permanece disponivel com historico. A Saga mostra reparoConcluidoEm.
A migration apenas adiciona essa data nullable no banco de Ordens. Documentos
Mongo anteriores leem os novos campos como ausentes/null ou listas vazias.
Nada e concluido ou consumido retroativamente sem a chamada explicita.

Domain, Application e InterfaceAdapters continuam sem pacotes externos.
Driver, session e transacao Mongo ficam somente em Infrastructure; a rota HTTP
fica na API, separada da controller Clean e do use case.

## Testar localmente

Com os tres repositorios lado a lado, Docker Desktop aberto, na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-conclusao-reparo.py
```

O roteiro usa apenas HTTP e dados novos. Abre OS, diagnostica, aprova/paga,
libera, bloqueia conclusao prematura, inicia e conclui o reparo. Confere baixa
unica, reserva encerrada, historico, exclusao da lista operacional e ausencia
de nova tentativa de pagamento. Repete conclusao e inicio apos finalizacao
para confirmar que nao ha nova baixa ou regressao.

No Postman, apos o roteiro de inicio do reparo, chame o novo POST sem body com
o token de Execucao. Consulte execucao, pecas e a Saga em Ordens ate Finalizada.

Nada cria recursos AWS. Compensacoes depois de pagamento/reserva e
estorno estao no [guia de compensacao paga](10-compensacao-paga.md).
BDD completo, cobertura minima obrigatoria e Sonar continuam pendentes.
