# Liberacao de Execucao apos pagamento

## O que muda

Pagamento confirmado nao significa que o reparo ja comecou. Ele autoriza
Ordens a pedir a liberacao da fila de Execucao. Os tres bancos continuam
independentes e nenhum servico consulta o banco de outro.

```text
Cobranca -> PagamentoConfirmado -> Ordens
  -> OS PagamentoConfirmado
  -> Saga LiberacaoExecucaoPendente + outbox LiberarExecucao
Execucao recebe LiberarExecucao
  -> confere entrada, diagnostico, vinculos e valor pago
  -> autorizacao + status AguardandoExecucao + resposta no mesmo documento
  -> ExecucaoLiberada -> Ordens
Ordens -> Saga ExecucaoLiberada + OS AguardandoExecucao
```

Durante a espera, a OS permanece PagamentoConfirmado. A confirmacao de
Execucao, nao o envio do comando, faz a OS avancar. Esse status novo da OS
usa o valor 10; valores anteriores nao foram renumerados.

Nao ha nova rota HTTP para informar pagamento ou forcar a liberacao. A
mensagem e uma autorizacao da Saga dentro da integracao interna, nao uma
prova criptografica do provedor financeiro. O isolamento e as permissoes
das filas deverao ser configurados na etapa de infraestrutura.

## Mensagens v1

LiberarExecucao usa execucao-comandos. Seu messageId e o ID estavel do pedido
salvo na Saga; causationId aponta para PagamentoConfirmado. SagaId e
correlationId continuam os mesmos desde a abertura da OS.

Payload obrigatorio:

```json
{
  "ordemServicoId": "ID_DA_OS",
  "execucaoId": "ID_DA_EXECUCAO",
  "orcamentoId": "ID_DO_ORCAMENTO",
  "pagamentoId": "ID_DO_PAGAMENTO",
  "provedorId": "ID_DA_TRANSACAO_MOCK",
  "valorTotal": 200.00,
  "moeda": "BRL"
}
```

IDs sao UUIDs, exceto provedorId, que e o identificador textual do provedor.
Campos de diagnostico nao sao aceitos nesse comando. Valor deve ser positivo,
ter no maximo duas casas e coincidir com servicos + quantidade de pecas vezes
preco unitario copiados durante o diagnostico, nao com o catalogo atual.

ExecucaoLiberada e LiberacaoExecucaoRecusada usam ordens-eventos. Preservam
os vinculos e o valor do pedido e apontam causationId para LiberarExecucao.
A recusa inclui codigo e motivo; a resposta aceita nao inclui esses campos.
Ordens confere todos os dados contra o pedido persistido antes de alterar a OS.

## Durabilidade e repeticao

- Ordens salva o resultado financeiro, a inbox, a Saga e o comando de
  liberacao em uma transacao MySQL. O worker publica a outbox depois.
- Execucao salva autorizacao, status, historico e resposta no mesmo documento
  Mongo, com compare-and-swap por versao. O worker publica a resposta depois.
- Uma reentrega do mesmo pedido usa a resposta persistida, sem nova
  autorizacao ou historico. Mesmo ID com outros dados e rejeitado.
- Marcar a resposta como publicada tambem incrementa a versao. Uma gravacao
  antiga nao consegue desfazer a publicacao ou regredir o estado.
- Ordens deduplica a resposta e salva inbox, Saga e historico da OS juntos.
  A confirmacao de pagamento repetida nao gera outro pedido nem reabre a Saga.
- Indisponibilidade temporaria de Execucao mantem o pedido pendente na fila.
  A politica de tentativas/DLQ ja existente continua valendo; este incremento
  nao adiciona um reconciliador para mensagens esgotadas.

Se o reparo nao puder ser liberado por regra de negocio, Execucao responde
LiberacaoExecucaoRecusada. Ordens fica em FalhaRequerIntervencao, preservando
o pagamento confirmado. A OS continua PagamentoConfirmado. Nao se presume
estorno, cancelamento ou nova cobranca: reconciliacao e compensacoes
financeiras ainda precisam de fluxo proprio.

## Testar localmente

Com os tres repositorios lado a lado, Docker Desktop aberto, na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-liberacao-execucao.py
```

O roteiro usa somente HTTP e dados novos. Cria servico e peca, abre OS,
conclui diagnostico, altera o preco atual do catalogo, aprova orcamento e
aguarda ExecucaoLiberada. Confere o valor original pago, os IDs da autorizacao,
o estoque intacto, o historico e a ausencia de cobranca repetida.

Para observar a espera pelo consumidor, siga o fluxo pelo Postman:

1. Abra a OS, conclua o diagnostico e espere OrcamentoGerado antes de pausar.
2. Execute `docker compose -f compose.integracao.yml stop execucao`.
3. Aprove o orcamento em Cobranca. Consulte a Saga ate LiberacaoExecucaoPendente.
   A OS deve estar PagamentoConfirmado. Anote pagamentoId e tentativas.
4. Execute `docker compose -f compose.integracao.yml start execucao`.
5. Aguarde ExecucaoLiberada. OS e Execucao devem estar AguardandoExecucao,
   com o mesmo pagamentoId e numero de tentativas de pagamento.

Nao deixe o consumidor parado por muito tempo e sempre religue ao terminar.
Esses comandos preservam os volumes. APIs: Ordens 18091, Execucao 18092 e
Cobranca 18093. O [guia anterior](05-pagamento-mock.md) explica os modos do mock.
Os roteiros anteriores de pagamento e de resposta ao orcamento foram
atualizados para aceitar o novo estado final.

## Limites deste incremento

Nao inicia o reparo, nao reserva nem consome pecas e nao finaliza a OS.
Nao altera o servico de Cobranca nem realiza estorno. A continuidade sera
reserva de estoque, inicio/conclusao do reparo e suas compensacoes.

Sagas historicas que ja estavam PagamentoConfirmado antes desta versao nao
recebem liberacao retroativa. A migration apenas adiciona os campos de
controle; nao reprocessa eventos financeiros. Nao existe endpoint de backfill.

Os testes incluem concorrencia, reentrega, resposta recusada, dados
divergentes, preservacao do preco do diagnostico e estoque. O roteiro HTTP
e a retomada local complementam as suites; nao substituem a etapa futura
de BDD completo, cobertura minima obrigatoria e Sonar. Nada cria recursos AWS.
