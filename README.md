# Oficina Mecanica - Ordens - Fase 4

Microsservico de atendimento e acompanhamento de ordens de servico. Este e o
primeiro incremento da Fase 4, em repositorio independente da entrega da Fase 3.

## O que esta implementado

- Login administrativo com JWT.
- Cadastro, consulta, atualizacao e desativacao de clientes e veiculos.
- Abertura de OS com CPF/CNPJ e veiculo existentes ou criando os dois cadastros
  na mesma chamada.
- Persistencia atomica do cadastro completo, da OS e do primeiro registro de
  historico no banco exclusivo de Ordens.
- Consulta por ID e listagem com prioridade por status, filtros e ordenacao.
- Swagger, healthchecks e logs JSON com `X-Correlation-ID` e identificacao do trace.
- Testes unitarios e de integracao, CI e relatorios de cobertura.

As OS novas ficam em `Recebida`. A abertura agora grava tambem a Saga e o
pedido de entrada em Execucao na mesma transacao. O worker publica esse pedido
quando habilitado. Este servico nao possui tabelas de estoque, orcamentos ou
pagamentos. Diagnostico, orcamento, pagamento mock e autorizacao da fila de
execucao ja estao conectados por mensagens. O reparo nao comeca automaticamente:
o comando administrativo em Execucao reserva as pecas e publica ReparoIniciado.

## Primeiro contrato de integracao

O [contrato v1 de abertura de OS e entrada em Execucao](docs/integracao/01-abertura-os.md)
define comandos, respostas, repeticao, falhas e compensacao do primeiro trecho
da Saga orquestrada. Esse trecho esta implementado. O
[guia da integracao local](docs/integracao/02-executar-abertura.md) explica como
subir o ambiente com SQS simulado, consultar a Saga e testar compensacao.
O [guia de diagnostico e orcamento](docs/integracao/03-diagnostico-orcamento.md)
documenta a integracao dos tres servicos, reentregas, recusas e o teste HTTP
automatizado. Nenhum desses comandos cria recursos AWS.

O [guia de aprovacao e recusa](docs/integracao/04-resposta-orcamento.md)
mostra como a resposta chega a Saga: aprovacao aguarda pagamento sem liberar
reparos; recusa solicita cancelamento em Execucao e aguarda a confirmacao.

O [guia de pagamento mock](docs/integracao/05-pagamento-mock.md) integra token,
chamada HTTP idempotente e resultado financeiro na Saga. A confirmacao fica
visivel na OS e permite solicitar a autorizacao de Execucao.

O [guia de liberacao de Execucao](docs/integracao/06-liberacao-execucao.md)
mostra a proxima etapa: a Saga envia LiberarExecucao apos pagamento confirmado
e aguarda a resposta antes de colocar a OS em AguardandoExecucao. Isso nao
inicia o reparo nem reserva ou consome pecas.

O [guia de inicio do reparo](docs/integracao/07-inicio-reparo.md) adiciona a
reserva transacional das pecas e o inicio explicito em Execucao. O evento
ReparoIniciado atualiza a Saga e a OS para EmExecucao. Falta de estoque
mantem a OS na fila, sem reserva parcial nem nova cobranca.

O [guia de conclusao do reparo](docs/integracao/08-conclusao-reparo.md) fecha
o trecho de execucao: o comando administrativo consome as reservas em Execucao
e ReparoConcluido finaliza a Saga e a OS, sem marcar entrega. Historico e
consulta por ID permanecem disponiveis; finalizadas saem da lista operacional.

## Clean Architecture

```text
src/
  OficinaMecanica.Ordens.Domain/             Entidades de negocio
  OficinaMecanica.Ordens.Application/        Use cases, inputs, outputs e contratos
  OficinaMecanica.Ordens.InterfaceAdapters/  Controllers Clean, gateways e presenters
  OficinaMecanica.Ordens.Infrastructure/     EF Core, MySQL, JWT e validadores externos
  OficinaMecanica.Ordens.Api/                Controllers HTTP e composicao no Program.cs
tests/
  OficinaMecanica.Ordens.UnitTests/
  OficinaMecanica.Ordens.IntegrationTests/
```

Domain, Application e InterfaceAdapters nao possuem pacotes externos. Os tres
sao projetos separados para que as dependencias sejam verificadas na compilacao.
Inputs, outputs e mappers ficam fora das pastas de use cases, com uma classe por
arquivo.

Fluxo de entrada:

```text
Controller HTTP -> Controller Clean -> Use case -> Interface do gateway
                                                        |
                              Gateway -> Interface da source -> Source EF -> MySQL
```

O `Program.cs` e o arquivo `DependencyInjection.cs` conectam as implementacoes aos
contratos. Os presenters convertem o output da aplicacao no DTO de resposta.
As anotacoes HTTP ficam somente na API; EF, JWT e BrazilianDocuments ficam nas
camadas externas.

## Executar com Docker Compose

Com o Docker Desktop aberto, na raiz deste repositorio:

```bash
docker compose --env-file .env.example up --build
```

Esse comando sobe um MySQL exclusivo, espera o banco ficar pronto, aplica as
migrations locais e inicia a API. Os valores de `.env.example` sao apenas para
desenvolvimento. A configuracao real podera ser fornecida por um `.env` ignorado
pelo Git ou pelas variaveis do ambiente de deploy.

- Swagger: <http://localhost:18081/swagger/index.html>
- Processo ativo: <http://localhost:18081/health>
- Conectividade com o banco: <http://localhost:18081/health/ready>
- MySQL local: `127.0.0.1:3307`, banco `ordens`, usuario `ordens_app`.

As portas podem ser alteradas com `API_PORT` e `MYSQL_PORT`. Para encerrar os
containers mantendo os dados:

```bash
docker compose --env-file .env.example down
```

## Desenvolver com dotnet run

Para executar somente o banco no Docker:

```bash
docker compose --env-file .env.example up -d mysql
```

Configure os valores locais com User Secrets, na raiz do repositorio:

```bash
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=127.0.0.1;Port=3307;Database=ordens;User=ordens_app;Password=LocalOrdens@123' --project src/OficinaMecanica.Ordens.Api
dotnet user-secrets set 'JwtSettings:SecretKey' 'chave-exclusiva-de-desenvolvimento-fase4-1234567890' --project src/OficinaMecanica.Ordens.Api
dotnet user-secrets set 'AdminUserSettings:Password' 'LocalAdminOrdens@123' --project src/OficinaMecanica.Ordens.Api
dotnet user-secrets set 'Database:ApplyMigrations' 'true' --project src/OficinaMecanica.Ordens.Api
dotnet run --project src/OficinaMecanica.Ordens.Api
```

O Swagger usa a mesma porta `18081`, portanto execute a API pelo Compose ou pelo
SDK em um dado momento.

## Primeiro teste no Postman

1. `POST /api/auth/login`, sem autenticacao:

```json
{
  "username": "admin",
  "password": "LocalAdminOrdens@123"
}
```

2. Use o `token` retornado em `Authorization: Bearer TOKEN` nas chamadas de cadastro
   e de OS. O exemplo abaixo cria cliente, veiculo e OS em `POST /api/ordensservico`:

```json
{
  "cliente": {
    "nome": "Cliente Demonstracao",
    "cpfCnpj": "52998224725",
    "email": "cliente@example.com",
    "telefone": "11999999999"
  },
  "veiculo": {
    "placa": "ABC1D23",
    "marca": "Fiat",
    "modelo": "Uno",
    "ano": 2020
  },
  "descricaoSolicitacao": "Revisar os freios"
}
```

Se cliente e veiculo ja existirem, use esta alternativa no mesmo endpoint:

```json
{
  "cpfCnpj": "52998224725",
  "veiculoId": "ID_DO_VEICULO",
  "descricaoSolicitacao": "Revisar os freios"
}
```

Misturar as alternativas retorna HTTP 400. CPF/CNPJ ou placa duplicados retornam
409. O cadastro completo e a OS sao confirmados na mesma transacao local.

3. Consulte `GET /api/ordensservico/ID_DA_OS` para ver os dados e o historico.

## Endpoints desta etapa

| Metodo | Rota | Uso |
|---|---|---|
| POST | `/api/auth/login` | Login administrativo |
| GET / POST | `/api/clientes` | Listar e cadastrar clientes |
| GET / PUT / DELETE | `/api/clientes/{id}` | Consultar, atualizar e desativar cliente |
| GET / POST | `/api/veiculos` | Listar e cadastrar veiculos |
| GET / PUT / DELETE | `/api/veiculos/{id}` | Consultar, atualizar e desativar veiculo |
| POST | `/api/ordensservico` | Abrir OS |
| GET | `/api/ordensservico/{id}` | Consultar OS e historico |
| GET | `/api/ordensservico?status=Recebida&ordenacao=maisAntigo` | Listar OS |
| GET | `/api/ordensservico/{id}/saga-abertura` | Acompanhar o registro ou a compensacao |
| POST | `/api/ordensservico/{id}/cancelar-entrada` | Solicitar compensacao antes do diagnostico |

O login e os healthchecks sao publicos. As operacoes de cadastro e OS exigem JWT
administrativo. `ordenacao` aceita `maisAntigo` ou `maisNovo`. A prioridade de
status e EmExecucao, AguardandoExecucao, PagamentoConfirmado, AguardandoPagamento,
AguardandoAprovacao, EmDiagnostico e Recebida. Finalizadas,
entregues e canceladas ficam fora da listagem operacional; a consulta por ID
preserva o acesso ao historico.

O login de cliente por CPF e a autorizacao de consultas do proprio cliente serao
integrados em uma proxima etapa. A API rejeita campos JSON desconhecidos para
evitar aceitar e ignorar dados que pertencem aos outros servicos.

## Testes

```bash
dotnet restore OficinaMecanica.Ordens.slnx
dotnet build OficinaMecanica.Ordens.slnx --configuration Release --no-restore
dotnet test OficinaMecanica.Ordens.slnx --configuration Release --no-build --no-restore
dotnet test OficinaMecanica.Ordens.slnx --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults
```

Os testes unitarios usam gateways em memoria. Os testes de integracao executam
o pipeline HTTP real com SQLite relacional em memoria, incluindo integridade
referencial e rollback. Eles nao precisam de Docker ou AWS; a validacao do
provider MySQL e da imagem Docker deve ser feita tambem com o Compose.

O CI inicia tambem um ElasticMQ descartavel para verificar envio, reentrega e
confirmacao usando o SDK SQS real. Com `SQS_TEST_ENDPOINT` ausente, somente o
teste de transporte e marcado como ignorado; as verificacoes de Saga e outbox
continuam funcionando sem fila.

O CI faz build e testes e publica os arquivos `coverage.cobertura.xml` como
artefato. A verificacao obrigatoria de 80%, o Sonar e o fluxo BDD distribuido
serao concluidos nas etapas de qualidade e integracao da Fase 4.

## Migrations

As migrations pertencem a Infrastructure deste servico. Para gerar uma nova:

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/OficinaMecanica.Ordens.Infrastructure --startup-project src/OficinaMecanica.Ordens.Api --output-dir Persistence/Migrations
```

Para aplicar pelo CLI, forneca a conexao do banco de Ordens:

```bash
export ConnectionStrings__DefaultConnection='Server=127.0.0.1;Port=3307;Database=ordens;User=ordens_app;Password=LocalOrdens@123'
dotnet ef database update --project src/OficinaMecanica.Ordens.Infrastructure --startup-project src/OficinaMecanica.Ordens.Api
```

No Compose, `Database__ApplyMigrations=true` automatiza essa etapa localmente.
No futuro deploy, a aplicacao das migrations sera uma etapa controlada da entrega.

## Proximos incrementos

- Entrega do veiculo e compensacoes das reservas, com regras explicitas.
- Reconciliacao, nova tentativa de negocio e compensacoes financeiras.
- Autenticacao por CPF e autorizacao por cliente.
- Terraform, Kubernetes, Kong e CD independente para a Fase 4.
- New Relic distribuido, qualidade no CI e evidencias finais.

Esta etapa nao cria recursos na AWS nem depende dos repositorios da Fase 3.
