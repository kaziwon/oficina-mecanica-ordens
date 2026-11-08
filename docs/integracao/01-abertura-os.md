# Contrato v1 - Abertura de OS e entrada em Execucao

## Estado deste documento

Este documento define o primeiro fluxo, agora implementado: publicacao e consumo
por SQS, Saga persistida em Ordens e respostas atomicas em Execucao. O
[guia de execucao local](02-executar-abertura.md) usa ElasticMQ como simulador SQS.
Nao foram criados recursos AWS. Os exemplos abaixo sao mensagens, nao endpoints
HTTP. O fluxo de Cobranca e os proximos estados ainda nao fazem parte desta entrega.

## Responsabilidades

- Ordens coordena a Saga e conhece o andamento do processo.
- Execucao controla sua fila e decide se pode registrar ou cancelar uma entrada.
- Cada servico grava somente no proprio banco.
- Cobranca e o mock de pagamento nao participam deste primeiro trecho.

```text
Cliente -> POST /api/ordensservico -> Ordens
                                      |
                              RegistrarExecucao
                                      |
                                      v
                                   Execucao
                                      |
                  ExecucaoRegistrada ou RegistroExecucaoRecusado
                                      |
                                      v
                                    Ordens
```

Entrar na fila nao e iniciar o diagnostico. A OS continua `Recebida`, e a
execucao nasce `AguardandoDiagnostico`. O inicio do diagnostico e outro passo.

## Mensagens

Um comando pede uma acao; um evento informa algo que ja aconteceu.

| Tipo | Emissor -> destinatario | Dados de negocio |
|---|---|---|
| `RegistrarExecucao` | Ordens -> Execucao | `ordemServicoId`, `descricaoSolicitacao` |
| `ExecucaoRegistrada` | Execucao -> Ordens | `ordemServicoId`, `execucaoId` |
| `RegistroExecucaoRecusado` | Execucao -> Ordens | `ordemServicoId`, `codigo`, `motivo` |
| `CancelarEntradaExecucao` | Ordens -> Execucao | `ordemServicoId`, `motivo` |
| `EntradaExecucaoCancelada` | Execucao -> Ordens | `ordemServicoId`, `execucaoId` opcional |
| `CancelamentoEntradaExecucaoRecusado` | Execucao -> Ordens | `ordemServicoId`, `codigo`, `motivo` |

Os tres primeiros tipos compoem a entrada na fila. Os tres ultimos definem a
compensacao desse trecho, sem autorizar o cancelamento de um reparo em curso.
Os canais logicos sao comandos de Execucao e eventos destinados a Ordens;
ambos precisam de controle de acesso entre os servicos, sem endpoint publico
que aceite uma confirmacao de negocio enviada por qualquer cliente.

### Envelope comum

| Campo | Regra |
|---|---|
| `messageId` | GUID da mensagem; nao muda ao reenviar a mesma mensagem |
| `type` | Um dos tipos definidos acima |
| `version` | Inteiro `1` para este contrato |
| `sagaId` | GUID do processo, gerado por Ordens e preservado nas respostas |
| `causationId` | ID da mensagem que causou esta; nulo no primeiro comando |
| `occurredAt` | Data e hora UTC em ISO 8601; nao define a ordem de processamento |
| `correlationId` | Identificador de correlacao da requisicao inicial |
| `payload` | Somente os dados do respectivo tipo |

`messageId` identifica uma entrega logica; `sagaId` identifica todo o processo.
Nao incluir JWT, credenciais, CPF/CNPJ ou dados cadastrais nestas mensagens.
A propagacao tecnica de trace sera configurada no transporte, separadamente
da identificacao de negocio da Saga.

Exemplo do comando:

```json
{
  "messageId": "11111111-1111-4111-8111-111111111111",
  "type": "RegistrarExecucao",
  "version": 1,
  "sagaId": "22222222-2222-4222-8222-222222222222",
  "causationId": null,
  "occurredAt": "2026-11-08T15:00:00Z",
  "correlationId": "abertura-os-demo-001",
  "payload": {
    "ordemServicoId": "33333333-3333-4333-8333-333333333333",
    "descricaoSolicitacao": "Revisar os freios"
  }
}
```

Exemplo da confirmacao:

```json
{
  "messageId": "44444444-4444-4444-8444-444444444444",
  "type": "ExecucaoRegistrada",
  "version": 1,
  "sagaId": "22222222-2222-4222-8222-222222222222",
  "causationId": "11111111-1111-4111-8111-111111111111",
  "occurredAt": "2026-11-08T15:00:01Z",
  "correlationId": "abertura-os-demo-001",
  "payload": {
    "ordemServicoId": "33333333-3333-4333-8333-333333333333",
    "execucaoId": "55555555-5555-4555-8555-555555555555"
  }
}
```

O destinatario valida tipo, versao, IDs, campos obrigatorios e limites de texto
antes de chamar um use case. A descricao segue o limite atual de 1000 caracteres.
Mensagens malformadas ou de versao desconhecida vao para quarentena, sem
alterar uma OS cujo processo nao possa ser identificado com seguranca.

## Persistencia e repeticao

1. Ordens gravara OS, historico inicial, Saga e comando pendente na mesma
   transacao MySQL. Se essa gravacao falhar, nada disso sera confirmado.
2. Um worker publicara o comando salvo. Este registro pendente e a **outbox**:
   permite retomar o envio se a fila estiver fora do ar.
3. Execucao gravara a entrada, a identificacao do processamento e a resposta
   pendente atomicamente. No MongoDB, essa unidade deve ficar em um documento,
   ou usar uma transacao em replica set; nao basta gravar colecoes separadas.
4. Outro envio publicara a resposta salva. Ordens registrara o resultado da
   Saga e a deduplicacao da resposta na mesma transacao local.

A entrega pode se repetir. O consumidor so confirma o recebimento depois da
persistencia; nenhum passo depende de uma promessa de entrega exatamente uma vez.
Falhar depois de enviar e antes de marcar a outbox tambem pode repetir o envio.

Repetir `RegistrarExecucao` para a mesma Saga/OS e os mesmos dados devolve a
mesma execucao, sem novo historico. Uma execucao criada manualmente so pode ser
reaproveitada apos validar a OS e a descricao; vinculo com outra Saga ou dados
divergentes devem gerar recusa, nunca sobrescrita silenciosa.

Ordens valida se a resposta corresponde a Saga, OS e comando esperados. Respostas
duplicadas nao repetem efeitos; respostas atrasadas nao reabrem uma Saga cancelada.
Confirmacoes de etapas distintas nao podem ser aceitas somente por terem o mesmo ID de OS.

## Falhas e compensacao

| Situacao | Tratamento previsto |
|---|---|
| Fila fora do ar | Manter a outbox e tentar novamente; nao perder a OS |
| Banco de Execucao indisponivel | Nao confirmar a mensagem; permitir nova tentativa |
| Resposta perdida | Republicar a resposta pendente, preservando seu `messageId` |
| Registro recusado com `ProcessoCancelado` | Registrar o motivo na Saga e cancelar a OS com historico |
| Recusa por vinculo conflitante ou codigo desconhecido | Exigir intervencao, sem cancelar trabalho possivelmente existente |
| Tentativas tecnicas esgotadas | Fila de mensagens mortas e intervencao/reprocessamento; nao fingir recusa de negocio |
| Cancelamento do processo antes do diagnostico | Enviar `CancelarEntradaExecucao` e aguardar confirmacao |
| Diagnostico ja iniciado | Recusar esta compensacao automatica e exigir tratamento explicito |

Timeout nao prova que Execucao deixou de registrar a entrada. Ordens nao deve
cancelar a OS definitivamente apenas porque nao recebeu uma resposta a tempo.
Se decidir abortar, deve persistir a compensacao e acompanhar sua conclusao.

Compensacao nao apaga dados: marca a entrada e a OS como canceladas e preserva
seus historicos. Nao exclui cliente ou veiculo criado junto da OS.

Se o cancelamento chegar antes do registro, Execucao precisa persistir uma
marca de cancelamento por Saga/OS. Um comando de registro atrasado nao pode
recriar essa entrada. Repetir o cancelamento deve devolver o mesmo resultado.

O estado da Saga e separado do status operacional da OS. Estados previstos
para este trecho: `AguardandoRegistro`, `RegistroConfirmado`,
`CompensacaoPendente`, `Compensada`, `RegistroRecusado` e `FalhaRequerIntervencao`.
`RegistroConfirmado` conclui apenas este trecho, nao a Saga completa da oficina.

## Clean Architecture e verificacao

Contratos de entrada e saida e regras de orquestracao ficarao em Application e
Domain. InterfaceAdapters fara a adaptacao das mensagens aos use cases. SDK de
fila, serializacao, workers e persistencia ficarao nas camadas externas.
Nao havera referencia entre os projetos ou acesso ao banco do outro servico.

A implementacao deste trecho e validada com:

- Abertura da OS gerando uma unica entrada na fila.
- Repeticao de comandos e respostas sem duplicar execucao ou historico.
- Reinicio do publicador recuperando mensagens pendentes.
- Falha de persistencia sem confirmacao parcial.
- Recusa definitiva preservando motivo e historico.
- Compensacao repetida, fora de ordem e depois do inicio do diagnostico.
- Correlacao da mesma Saga nos logs dos dois servicos.

O teste do fluxo distribuido vira a base do BDD exigido na Fase 4. Estes casos
completos, incluindo Cobranca e pagamento, ainda serao implementados. Os testes
deste incremento cobrem somente a abertura e compensacao da entrada na fila.
