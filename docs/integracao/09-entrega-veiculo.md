# Entrega do veiculo

## Finalizada nao e Entregue

A conclusao do reparo deixa a OS Finalizada. Registrar a retirada do veiculo
e uma operacao administrativa separada de atendimento:

```text
POST /api/ordensservico/{id}/entregar (JWT Admin, sem body)
  -> controller HTTP -> controller Clean -> EntregarOrdemServicoUseCase
  -> regra da entidade: Finalizada -> Entregue
  -> gateway -> source EF -> status e historico na mesma transacao MySQL
```

O endpoint retorna 200 com a OS entregue. Repetir uma entrega concluida retorna
o registro persistido, com a mesma data e sem novo historico. ID inexistente
retorna 404, falta de login retorna 401 e qualquer status anterior a Finalizada
(ou Cancelada) retorna 409. A classe HTTP exige perfil administrativo.

Nao recebe status ou data no body. A data UTC e produzida pela aplicacao e
fica no novo registro Entregue do historico existente. Nao ha nova migration
nem um novo campo entregueEm; a consulta por ID ja mostra a data no historico.

## Responsabilidade de Ordens

Somente Ordens muda de Finalizada para Entregue. Execucao continua Finalizada,
com suas reservas ja consumidas; pagamento e estoque nao sao modificados.
Tambem nao cria outro comando financeiro ou nova mensagem de integracao.

A Saga permanece Finalizada, pois ja concluiu o fluxo distribuido de reparo.
Retirada fisica nao exige outra operacao nos demais servicos. Uma confirmacao
de conclusao repetida ou atrasada nao faz a OS entregue voltar para Finalizada.
Compensacoes financeiras e cancelamento depois de pagamento/reserva possuem
[fluxo proprio](10-compensacao-paga.md), antes da conclusao, e nao fazem parte da entrega.

OS entregue continua fora da listagem operacional, como ja ocorria com
Finalizada e Cancelada. GET /api/ordensservico/{id} preserva cadastro, descricao
e todos os registros de historico, incluindo conclusao e entrega.

## Concorrencia e rollback

A regra de negocio fica na entidade e o use case depende apenas de
IOrdemServicoGateway. Gateway e interface da source nao usam framework.
A persistencia especifica fica em EfOrdemServicoDataSource, na Infrastructure.

A source abre uma transacao e atualiza o status somente se o registro ainda
esta Finalizada. Apenas a chamada que muda esse status insere o historico de
entrega. Se a insercao do historico falhar, a mudanca de status tambem e
desfeita. Uma copia antiga da OS nao consegue gravar uma segunda entrega.

Quando outra chamada ja entregou a OS, o use case consulta novamente e retorna
o estado persistido. A resposta apos salvar tambem usa uma nova leitura,
mantendo a data consistente com a precisao do banco e com as repeticoes.
Nao sobrescreve o cadastro ou o historico inteiro com uma copia desatualizada.

## Testar localmente

Com os tres repositorios lado a lado e Docker Desktop aberto, na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-entrega-veiculo.py
```

O roteiro cria dados novos e percorre a OS ate a conclusao do reparo. Verifica
bloqueio antes da finalizacao, falta de login e ID inexistente. Depois faz
seis chamadas simultaneas de entrega contra o MySQL local e uma repeticao.
Confere historico unico, mesma data, consulta por ID e preservacao da Saga,
Execucao, reservas, estoque e pagamento.

No Postman, apos Finalizada, use o JWT administrativo de Ordens e chame o POST
sem body. Depois consulte a OS pelo ID: o status deve ser Entregue e o
historico deve ter apenas um registro desse status, mesmo repetindo o POST.

Os testes automatizados tambem verificam a disputa entre duas copias da OS,
rollback quando o historico nao e salvo e conclusao reentregue depois da entrega.
Nenhum comando deste guia cria infraestrutura AWS.
