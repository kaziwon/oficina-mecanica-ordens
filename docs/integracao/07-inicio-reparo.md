# Reserva de pecas e inicio do reparo

## Fluxo

Pagar e liberar a fila nao comeca o trabalho. O administrador inicia o reparo
em Execucao depois da autorizacao da Saga:

```text
OS e Execucao AguardandoExecucao
  -> POST /api/execucoes/{id}/iniciar-reparo (JWT Admin, sem body)
  -> transacao local Mongo: reservas + EmExecucao + historico + outbox
  -> evento ReparoIniciado na fila ordens-eventos
  -> Ordens: inbox + Saga EmExecucao + OS EmExecucao + historico
```

Ordens continua sendo a autoridade que libera o reparo apos pagamento. Execucao
faz a operacao de oficina e comunica o inicio real. Nao se acessa o banco de
outro servico e nao ha transacao distribuida entre Mongo e MySQL.

## Reserva nao e consumo

As pecas exibem tres quantidades:

- quantidadeEstoque: saldo fisico, ainda sem consumo do reparo.
- quantidadeReservada: separado para execucoes iniciadas.
- quantidadeDisponivel: estoque menos reservado, utilizavel por outra OS.

O inicio usa as quantidades do diagnostico ja aprovado/pago, nao um body com
pecas novas. A copia dos precos permanece a mesma. Cada execucao guarda suas
reservasPecas com pecaInsumoId, quantidade e reservadaEm, alem de reparoIniciadoEm.
Execucoes somente com servicos iniciam com uma lista de reservas vazia.

Saidas manuais so podem retirar o disponivel. Uma peca com reserva nao pode
ser desativada. Atualizar nome/preco nao altera reservas. Dados historicos sem
o novo contador sao lidos como zero; nao ha reserva retroativa.

## Atomicidade e concorrencia

Reservar varias pecas modifica varios documentos. MongoInicioReparoDataSource
usa a transacao do driver no banco exclusivo de Execucao, com snapshot e
majority. O gateway interno nao conhece Mongo, session ou framework.
As regras de status e disponibilidade ficam nas entidades/use cases.

Se faltar a segunda peca, a primeira reserva tambem e desfeita. Execucao fica
AguardandoExecucao, sem historico ou evento falso de inicio. A API retorna 409.
Repor o saldo permite repetir o mesmo endpoint sem solicitar novo pagamento.
Pecas inexistentes ou inativas tambem impedem o inicio completo.

O controle de versao da execucao e a transacao protegem inicios concorrentes.
Pedidos repetidos depois do sucesso retornam o estado salvo, com a mesma data
e sem duplicar reservas ou historico. Duas OS disputando o ultimo saldo nao
conseguem reservar mais que o estoque. O worker publica a outbox apos o commit;
marcar publicacao tambem muda a versao para impedir sobrescrita por copia antiga.

Esse rollback local nao e uma compensacao financeira da Saga: nenhuma reserva
parcial foi confirmada e o pagamento continua valido. Cancelamento depois do
inicio, estorno e liberacao/consumo de reservas precisam de fluxos explicitos
que serao implementados nos proximos incrementos.

## Evento v1

ReparoIniciado preserva sagaId e correlationId da abertura; causationId aponta
para LiberarExecucao. O payload identifica ordemServicoId, execucaoId,
orcamentoId, pagamentoId, provedorId, valorTotal, moeda BRL e reparoIniciadoEm.
A data e a mesma de occurredAt. Nao aceita itens, diagnostico ou motivo de recusa.

Ordens confere todos esses vinculos contra a autorizacao persistida. O evento
atualiza inbox, Saga, OS e historico em uma transacao MySQL. Reentrega nao
reinicia a OS, nao cobra novamente e nao repete historico.

Uma fila padrao pode entregar ReparoIniciado antes de ExecucaoLiberada. Nesse
caso Ordens nao confirma a mensagem nem avanca a OS: a reentrega ocorre apos
a confirmacao da liberacao. Uma resposta de liberacao atrasada nao faz uma OS
EmExecucao voltar para AguardandoExecucao. A politica de DLQ existente permanece;
um reconciliador de mensagens esgotadas ainda nao faz parte deste incremento.

## Mongo local e CI

Transacoes entre documentos exigem replica set. O Compose prepara rs0 de
um no automaticamente, com autenticacao e keyfile em volume separado. Os
servicos mongo-keyfile e mongo-replica-init sao auxiliares e encerram apos a
preparacao. Isso habilita transacoes, mas nao oferece alta disponibilidade.

O volume de dados existente e preservado. Nao use down -v para atualizar e
nao troque credenciais de um volume ja inicializado. Para acesso no host,
acrescente replicaSet=rs0&directConnection=true a URI; o segundo parametro
permite usar a porta local mesmo com o nome interno mongo no replica set.
O [guia oficial de conexao](https://www.mongodb.com/docs/drivers/csharp/current/connect/connection-targets/)
explica essa configuracao para desenvolvimento em Docker.

O CI de Execucao tambem prepara um replica set descartavel antes dos testes.
Nao adiciona Secrets, nao chama a AWS e nao implanta nada. Um Mongo standalone
antigo nao suporta o novo inicio transacional; atualize o Compose primeiro.

## Testar

Com os tres repositorios atualizados lado a lado, na raiz de Ordens:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-inicio-reparo.py
```

O roteiro cria OS nova, verifica bloqueio antes da autorizacao, aprova/paga,
tenta iniciar com saldo insuficiente, repoe estoque e inicia. Confere os dois
servicos EmExecucao, reservas, protecao contra saida/desativacao e ausencia de
nova cobranca ou historico duplicado nas repeticoes. Nao precisa de AWS.

No Postman, depois de chegar a ExecucaoLiberada, chame o novo POST sem body
com o token de Execucao. Consulte execucao e pecas; em Ordens, consulte OS e
saga-abertura ate EmExecucao. A atualizacao entre servicos e assincrona.

Finalizacao do reparo, consumo das reservas, compensacoes, BDD completo,
cobertura minima obrigatoria e Sonar continuam nas proximas etapas.
