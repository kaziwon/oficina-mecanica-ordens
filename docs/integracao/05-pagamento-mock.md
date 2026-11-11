# Pagamento mock integrado a Saga

## O que foi implementado

Este e o mock autorizado pelo professor: Cobranca usa client_id/client_secret
para obter um token e depois chama um servico HTTP de pagamento. Nao ha
movimentacao financeira, chamada ao Mercado Pago nem credencial real.
O contrato do mock e simplificado e proprio; trocar a URL nao o transforma em
integracao real com Mercado Pago. Nunca coloque credenciais reais neste mock.

O servidor de teste fica em tools/OficinaMecanica.PagamentoMock, no repositorio
de Cobranca. Nao e um quarto microsservico de negocio ou um novo repositorio.
Ele usa SQLite em um volume local para lembrar as chaves de idempotencia mesmo
apos reiniciar. A aplicacao de Cobranca continua usando seu MySQL exclusivo.

```text
Cliente aprova orcamento em Cobranca
  -> evento OrcamentoAprovado -> Ordens
  -> Saga AguardandoPagamento + comando SolicitarPagamento na outbox
  -> Cobranca: pagamento Pendente + inbox, na mesma transacao MySQL
  -> worker de pagamento: POST /oauth/token no mock
  -> POST /payments com Bearer e X-Idempotency-Key = ID do pagamento
  -> MySQL: resultado do pagamento + evento na outbox
  -> evento PagamentoConfirmado / PagamentoRecusado / FalhaProcessamentoPagamento
  -> Ordens: Saga, inbox e historico na mesma transacao
```

O JWT administrativo de /api/auth/login e o token de /oauth/token tem usos
diferentes: o primeiro autoriza chamadas nas APIs da oficina; o segundo e usado
por Cobranca para chamar o provedor simulado. Os dados de autenticacao ficam na
configuracao externa, nao no dominio ou em um body enviado pelo cliente da OS.

## Clean Architecture

O use case ProcessarPagamentoUseCase depende de IPagamentoProvedorGateway.
O gateway da camada de adapters apenas delega a uma source abstrata. HttpClient,
JSON HTTP, credenciais e configuracao ficam na source de Infrastructure.
O servidor mock de teste fica fora das camadas da aplicacao.

O dominio calcula o valor a partir do orcamento aprovado. O comando identifica
a aprovacao original, que Cobranca confere em sua propria outbox. O chamador
nao escolhe valor, moeda ou resultado financeiro arbitrarios. O endpoint manual
de pendencia continua disponivel, mas nao permite marcar um pagamento como pago.

## Durabilidade, repeticao e falhas

- A pendencia e salva antes do HTTP. O worker encontra trabalhos pendentes
  depois de um reinicio; a mensagem SQS nao precisa permanecer em processamento.
- O mesmo pagamento sempre usa a mesma chave. O mock persiste uma transacao por
  chave e rejeita a reutilizacao com outro valor. Mudar seu modo nao altera uma
  transacao ja persistida. Nao apague o volume para tentar mudar um resultado.
- Se o HTTP confirmar e a gravacao MySQL falhar, a proxima tentativa repete a
  mesma chave e recupera a mesma transacao, sem criar outra cobranca.
- Versao otimista e transacao local protegem resultado e outbox contra duas
  instancias processando a mesma pendencia. Mensagens continuam sendo entregues
  pelo menos uma vez, com deduplicacao nos consumidores.
- Timeout, 503, token invalido ou resposta divergente nao comprovam recusa.
  A pendencia recebe nova tentativa, sem inventar aprovacao ou cancelamento.
- Apos o limite tecnico, o resultado fica FalhaRequerIntervencao, com codigo
  ResultadoNaoConfirmado. Pode haver uma transacao externa nao reconciliada;
  portanto nao ha nova chave automatica, cancelamento ou estorno presumido.
- Uma recusa conhecida do mock fica Recusado e nao e repetida como nova cobranca.
  Recusa e esgotamento tecnico deixam a Saga em FalhaRequerIntervencao e a OS
  em AguardandoPagamento, sem liberar reparos. Reconciliacao, nova tentativa de
  negocio e compensacoes financeiras ainda precisam de fluxo proprio.

Pagamento confirmado deixa Saga e OS em PagamentoConfirmado (novo valor 9 da
OS, sem renumerar os anteriores). Execucao ainda fica AguardandoLiberacao:
autorizar a fila de reparo sera o proximo incremento. A listagem prioriza
EmExecucao, PagamentoConfirmado, AguardandoPagamento, AguardandoAprovacao,
EmDiagnostico e Recebida. A API de pagamento mostra provedorId, tentativas,
codigoFalha e finalizadoEm. A Saga mostra o ID e a confirmacao do pagamento.

## Mensagens v1

SolicitarPagamento usa a fila cobranca-comandos. Seu causationId aponta para
OrcamentoAprovado. Os eventos de resultado usam ordens-eventos e apontam para
o comando SolicitarPagamento. SagaId e correlationId originais sao preservados.

O payload do comando tem ordemServicoId, execucaoId, orcamentoId, valorTotal
e moeda BRL. O resultado inclui pagamentoId e, se conhecido, provedorId.
Recusa e falha tecnica incluem codigo/motivo. Dados de diagnostico nao sao
aceitos. Valores, causas e IDs divergentes nao alteram a Saga.

## Executar e demonstrar localmente

Com os tres repositorios lado a lado, nas versoes compativeis, abra Docker
Desktop e execute na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-pagamento-mock.py
```

O Compose inclui o mock em http://localhost:18094, com /health publico. As
APIs continuam nas portas 18091, 18092 e 18093. O pequeno servico de permissoes
prepara o volume SQLite e encerra; o mock roda sem root. As credenciais
explicitas do Compose sao ficticias e somente de desenvolvimento.

O script abre uma OS, conclui diagnostico, gera/aprova orcamento e aguarda o
resultado financeiro. Depois consulta o mock e repete a chamada com a mesma
chave para comprovar o mesmo ID, sem liberar Execucao.

Para recusar somente novas transacoes:

```bash
PAGAMENTO_MOCK_RESULTADO=rejected docker compose -f compose.integracao.yml up -d pagamento-mock
python3 infra/local/testar-pagamento-mock.py --esperado rejected
```

Para simular indisponibilidade e esgotamento das tentativas:

```bash
PAGAMENTO_MOCK_RESULTADO=unavailable docker compose -f compose.integracao.yml up -d pagamento-mock
python3 infra/local/testar-pagamento-mock.py --esperado unavailable
```

Restaure o modo normal ao terminar:

```bash
PAGAMENTO_MOCK_RESULTADO=approved docker compose -f compose.integracao.yml up -d pagamento-mock
```

Cada execucao do roteiro cria uma OS nova. O Compose permite 3 tentativas com
intervalo de 2 segundos; o padrao da aplicacao e 5 com intervalo de 10 segundos.
Os roteiros anteriores continuam disponiveis. O de resposta ao orcamento
agora aceita que o pagamento automatico avance antes da consulta.

### Postman

1. Gere e aprove um orcamento seguindo os guias anteriores.
2. Consulte a Saga em Ordens ate PagamentoConfirmado ou FalhaRequerIntervencao.
3. Com o JWT administrativo de Cobranca, consulte GET /api/orcamentos/ID para
   obter pagamentoId, depois GET /api/pagamentos/ID_DO_PAGAMENTO.
4. Confira que Execucao continua AguardandoLiberacao e que os IDs financeiros
   e valores coincidem com a Saga e com o orcamento.

## Configuracao e limites

PagamentoMock:Enabled e falso por padrao. O Compose integrado habilita o worker
e informa BaseUrl, ClientId e ClientSecret. Nao e necessario cadastrar Secrets
no GitHub para estes testes: o CI executa o mock em processo e o MySQL local ao
runner. O teste de envio/reentrega usa ElasticMQ, como nos incrementos anteriores.

As migrations aplicam-se automaticamente no Compose. Aprovacoes historicas e
pendencias manuais nao vinculadas nao sao cobradas retroativamente: somente
novos comandos validos da Saga entram no worker. Nao existem ainda webhook de
pagamento real, estorno, reconciliacao operacional, liberacao de reparo ou BDD
do fluxo completo. Infraestrutura AWS e observabilidade distribuida ficam para
as etapas posteriores. Nada deste roteiro cria recursos na AWS.
