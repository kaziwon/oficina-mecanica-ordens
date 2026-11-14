# Recuperacao financeira e de mensagens

## Consultar nao e cobrar novamente

Uma falha HTTP pode ocorrer depois do efeito financeiro. Por isso, apos esgotar
tentativas, o estado FalhaRequerIntervencao nao e tratado como recusa nem autoriza
nova cobranca. O mesmo se aplica ao estorno.

Com JWT Admin de Cobranca:

- POST /api/pagamentos/{id}/reconciliar
- POST /api/pagamentos/{id}/estorno/reconciliar

Sem body ou com {}. Campos adicionais sao rejeitados. O operador nao escolhe
resultado, valor ou identificador externo. Os use cases consultam o provedor
por gateway/source usando o ID ORIGINAL da operacao; HTTP/OAuth so existem em
Infrastructure. Nao ha POST financeiro na reconciliacao, apenas token + GET.

Resposta 204 confirma o processamento da consulta; use o GET existente para
ler o estado persistido, inclusive se continua incerto. Resultado conhecido e persistido junto ao
novo evento outbox, com concorrencia otimista. ConsultasReconciliacao,
UltimaConsultaReconciliacaoEm e ReconciliadoEm ficam no proprio registro,
separadas de Tentativas. Nova migration acrescenta esses campos a Pagamentos e
Estornos. Nao altera registros anteriores nem apaga a cobranca original.

- GET do provedor com resultado final e prova correta: confirma ou registra recusa.
- GET 404: audita consulta, mas mantem intervencao, sem publicar recusa.
- Indisponivel/resposta divergente: 503, sem inventar resultado ou efeito financeiro.
- Ainda Pendente: 409; aguarde o worker.
- ID inexistente: 404.
- Ja confirmado/recusado: retorna o estado salvo sem consulta ou outro evento.

Ordens aceita resultado conhecido depois da falha incerta da MESMA operacao.
Confirmacao de pagamento libera Execucao uma vez. Confirmacao de estorno conclui
o cancelamento uma vez. Falhas tecnicas antigas sao consumidas sem regredir o
resultado final; resultados financeiros contraditorios continuam rejeitados.
Uma recusa comprovada nao pode ser transformada em aprovacao pelo operador.

## Teste HTTP completo, somente local

Com os tres repositorios lado a lado, na raiz de Ordens:

```bash
PAGAMENTO_MOCK_RESULTADO=approved_response_lost PAGAMENTO_MOCK_ESTORNO_RESULTADO=refunded_response_lost docker compose -f compose.integracao.yml up -d --build
python3 infra/local/testar-reconciliacao.py
```

O script cria OS e diagnostico, aprova orcamento, aguarda falha financeira,
reconcilia pagamento, solicita cancelamento, aguarda falha do estorno e o
reconcilia. Confere mesmos IDs, tentativas preservadas, repeticao sem novo
resultado e OS cancelada somente depois da confirmacao. Nao acessa bancos.

Volte ao modo normal, preservando os volumes:

```bash
docker compose -f compose.integracao.yml up -d
```

## Inspecionar e reprocessar DLQ

Instale as dependencias locais conforme o guia 14. Por padrao, o comando abaixo
somente inspeciona ate uma mensagem e a torna visivel novamente na DLQ:

```bash
.venv-tests/bin/python infra/local/reprocessar_dlq.py --fila execucao-comandos
```

Depois de corrigir a causa da falha, para reenviar ate cinco mensagens:

```bash
.venv-tests/bin/python infra/local/reprocessar_dlq.py --fila execucao-comandos --limite 5 --confirmar
```

Tambem aceita cobranca-comandos e ordens-eventos. O SDK usa credenciais ficticias
explicitas e so aceita endpoint HTTP de loopback; nao usa perfil AWS do usuario.
O teste do SDK usa filas temporarias isoladas, nao limpa as filas da aplicacao.

Body, messageId de negocio, causa e correlacao sao preservados EXATAMENTE.
O ID de transporte atribuido pelo SQS muda, mas a inbox usa o ID do envelope.
So apaga da DLQ depois do ACK de envio. Falha ao enviar mantem a mensagem;
falha ao apagar pode duplicar entrega, coberta pela mesma identidade de negocio.
Envelope invalido ou versao/tipo desconhecido nao e reenviado nem apagado.
Saida nao mostra payload, CPF ou credenciais. O replay nao corrige uma mensagem
semanticamente errada e nao substitui a consulta financeira. Se a causa nao foi
resolvida, a mensagem pode voltar a DLQ.

## Limites intencionais

Nao forca aprovacoes, nao cria outra cobranca apos recusa comprovada, nao faz
reembolso parcial nem desfaz reparo ja concluido. Esses casos exigem uma decisao
de negocio adicional, nao uma retentativa tecnica. Contas externas, AWS, Sonar
e pipelines de deploy nao foram configurados nesta etapa.
