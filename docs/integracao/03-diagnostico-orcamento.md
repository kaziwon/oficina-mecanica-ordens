# Diagnostico e geracao automatica de orcamento

## O que esta conectado

```text
Execucao: iniciar/concluir diagnostico
  -> MongoDB: estado + evento pendente no mesmo documento
  -> fila ordens-eventos
Ordens: atualizar Saga e historico da OS
  -> MySQL de Ordens: Saga + inbox + pedido na outbox
  -> fila cobranca-comandos
Cobranca: validar snapshot e calcular total
  -> MySQL de Cobranca: orcamento + inbox + resposta na outbox
  -> fila ordens-eventos
Ordens: confirmar orcamento e mudar OS para AguardandoAprovacao
```

Ordens continua sendo o orquestrador. Execucao nao chama Cobranca diretamente.
Cada servico acessa apenas seu proprio banco. Os precos, nomes e quantidades
viajam como uma copia do diagnostico, nao como consultas ao banco de Execucao.
Nao existe transacao distribuida entre MySQL e MongoDB.

## Mensagens v1

Todas usam o envelope de abertura: `messageId`, `type`, `version`, `sagaId`,
`causationId`, `occurredAt` UTC, `correlationId` e `payload`.

| Tipo | Origem -> destino | Payload adicional a ordemServicoId e execucaoId |
|---|---|---|
| DiagnosticoIniciado | Execucao -> Ordens | diagnosticoIniciadoEm |
| DiagnosticoConcluido | Execucao -> Ordens | diagnosticoIniciadoEm, diagnosticoConcluidoEm, diagnostico, itens |
| GerarOrcamento | Ordens -> Cobranca | diagnostico, itens |
| OrcamentoGerado | Cobranca -> Ordens | orcamentoId, valorTotal |
| GeracaoOrcamentoRecusada | Cobranca -> Ordens | codigo, motivo |

Cada item tem `tipo` (`Servico` ou `PecaInsumo`), `referenciaId`, `nome`,
`quantidade` e `precoUnitario`. A quantidade de um servico e 1 no diagnostico.
O valor total e calculado por Cobranca; Ordens nao envia um total para confiar.

Os eventos de diagnostico apontam em `causationId` para o pedido original
`RegistrarExecucao`, que vincula Execucao a Saga. O comando `GerarOrcamento`
aponta para o evento de conclusao. A resposta de Cobranca aponta para esse
comando. `correlationId` e `sagaId` sao preservados em todo esse percurso.

## Estados e falhas

- Inicio: OS `Recebida -> EmDiagnostico`; Saga `EmDiagnostico`.
- Conclusao: Saga `OrcamentoPendente`; a OS ainda esta `EmDiagnostico`.
- Orcamento confirmado: Saga `OrcamentoGerado`; OS `AguardandoAprovacao`.
- Recusa de geracao: Saga `FalhaRequerIntervencao`, com motivo. Nao apaga
  diagnostico, nao cancela trabalho realizado e nao inventa aprovacao.
- `DadosInvalidos`: snapshot sem itens, precos invalidos, duplicatas ou outras
  regras de orcamento. A recusa tambem grava inbox e outbox atomicamente.
- `ConflitoDeVinculo`: outro pedido ja vinculou a OS ou execucao a um orcamento.
  Um orcamento criado manualmente nao e adotado silenciosamente pela Saga.

Reentregas usam IDs persistidos: nao geram novo pedido nem novo orcamento.
Cobranca compara tambem o hash do pedido para rejeitar reutilizacao do mesmo
`messageId` com outro conteudo. Chaves unicas protegem OS e execucao contra
criacao concorrente de orcamentos. Falhas tecnicas nao viram recusa de negocio:
o worker nao confirma a mensagem, permitindo retentativa e depois DLQ.

A conclusao inclui o inicio do diagnostico: se chegar primeiro, Ordens consegue
registrar o historico sem depender da ordem da fila. Inicio e confirmacao de
registro atrasados nao fazem a Saga ou a OS voltar a um estado anterior.
Uma Saga compensada nao e reaberta por eventos tardios.

Execucoes criadas manualmente, sem Saga, preservam o comportamento independente
e nao publicam eventos de diagnostico para uma Saga inexistente. Nao ha backfill
automatico de diagnosticos concluidos antes desta versao.

O cancelamento de entrada continua limitado ao trecho anterior ao diagnostico.
Quando Ordens ja conhece seu inicio, retorna HTTP 409. Se o comando estava em
transito, Execucao recusa a compensacao e a Saga exige intervencao. Orcamentos
recusados nesta etapa precisam de tratamento manual; ainda nao ha API de correcao
ou reprocessamento financeiro. Isso sera definido nas proximas etapas da Saga.

## Executar localmente

Deixe os tres repositorios lado a lado, nas branches compativeis, abra Docker
Desktop e execute na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
```

Isso inicia as tres APIs, dois MySQL separados, MongoDB e ElasticMQ. Nao cria
recursos AWS. Se necessario, configure `EXECUCAO_PATH` e `COBRANCA_PATH` com os
caminhos dos outros repositorios antes do comando. As migrations SQL sao
aplicadas pelas proprias APIs neste ambiente de desenvolvimento.

| Servico | Swagger |
|---|---|
| Ordens | http://localhost:18091/swagger/index.html |
| Execucao | http://localhost:18092/swagger/index.html |
| Cobranca | http://localhost:18093/swagger/index.html |

Use `POST /api/auth/login` em cada API, com `username=admin` e
`password=LocalAdminSaga123`, e passe seu JWT como Bearer. As credenciais do
Compose sao exclusivamente locais. O banco de Cobranca esta em `127.0.0.1:3310`.

Para testar automaticamente pelo HTTP:

```bash
python3 infra/local/testar-abertura.py
python3 infra/local/testar-diagnostico-orcamento.py
```

O segundo script cadastra um servico de R$ 100,00 e uma peca de R$ 50,00,
abre uma OS e conclui o diagnostico com um servico e duas pecas. Confere o
orcamento de R$ 200,00, os IDs e a mudanca da OS. Depois testa a recusa de
um diagnostico sem itens. Os scripts criam dados ficticios e nao consultam
bancos diretamente. O primeiro continua testando abertura e compensacao.

No Postman, o trecho novo e:

1. Criar servico/peca nas APIs de catalogo de Execucao.
2. Criar OS em Ordens e consultar `/api/ordensservico/ID/saga-abertura` ate
   receber `RegistroConfirmado` com `execucaoId`.
3. Em Execucao, chamar `POST /api/execucoes/ID/iniciar-diagnostico`.
4. Chamar `POST /api/execucoes/ID/concluir-diagnostico`:

```json
{
  "diagnostico": "Substituir pastilhas",
  "servicoIds": ["ID_DO_SERVICO"],
  "pecasInsumos": [{ "pecaInsumoId": "ID_DA_PECA", "quantidade": 2 }]
}
```

5. Acompanhar a Saga ate `OrcamentoGerado`, com `orcamentoId` e `valorOrcamento`.
6. Em Cobranca, consultar `GET /api/orcamentos/ID_DO_ORCAMENTO` e conferir itens,
   valor e status `AguardandoAprovacao`. Em Ordens, conferir o novo status da OS.

Os exemplos `ID_*` devem ser substituidos por GUIDs reais retornados pelas APIs.

Para parar sem apagar os volumes:

```bash
docker compose -f compose.integracao.yml down
```

Se o simulador ja estava ligado antes da criacao da fila de Cobranca, reinicie
somente ele para ler a configuracao atualizada:

```bash
docker compose -f compose.integracao.yml restart elasticmq
```

## Limite deste incremento

O [incremento seguinte](04-resposta-orcamento.md) integra a aprovacao/recusa do
orcamento na Saga. Ainda faltam pagamento mock, reserva de pecas, liberacao e
finalizacao dos reparos e compensacoes financeiras. Registrar uma pendencia de
pagamento ainda e local a Cobranca. O teste HTTP deste trecho nao substitui o
BDD do fluxo completo exigido na entrega final.
