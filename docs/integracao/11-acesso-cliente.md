# Acesso do cliente por CPF

## Escopo e limite de seguranca

Ordens possui o cadastro do cliente e o vinculo com a OS. Por isso emite o
token e oferece consultas proprias, sem ler o banco de Execucao ou Cobranca.
Os repositorios da Fase 3, inclusive a Lambda, nao foram alterados ou implantados.
A consulta e a resposta do orcamento pelo cliente estao no
[incremento seguinte](12-orcamento-cliente.md). Os endpoints administrativos
de Cobranca continuam fechados para este token.

CPF sozinho NAO comprova identidade: quem conhece o CPF de outra pessoa pode
solicitar o token dela neste fluxo academico, como na proposta da Fase 3.
Checksum valido nao e autenticacao forte. Nao exponha este login como solucao
de producao. Um ambiente real exige prova adicional de identidade, por exemplo
um provedor de identidade ou verificacao por canal confiavel, alem de HTTPS.

Assinatura, audience, perfil e filtro por dono impedem adulterar o token e
usar o token de um cliente para consultar a OS de outro. Essas protecoes NAO
resolvem a limitacao de identificar alguem somente por CPF. Use dados ficticios.

## Login e consultas

```http
POST /api/auth/cliente
Content-Type: application/json

{"cpf":"52998224725"}
```

Aceita 11 digitos ASCII ou o formato 529.982.247-25. Rejeita CNPJ, letras,
checksum incorreto e formatos arbitrarios. O use case chama a validacao pelo
ICpfCnpjValidatorGateway, implementado na Infrastructure. O CPF nao vai na URL
nem no JWT. O body nao aceita role, clienteId ou campos extras.

Retorna 200 com clienteId, nome, token e expiresAt. Documento invalido retorna
400; cadastro ausente ou desativado recebe o mesmo 401, sem token. Exceder o
limite de tentativas retorna 429. Guarde o token em uma variavel separada do
administrativo, por exemplo tokenCliente.

Com Authorization: Bearer TOKEN_DO_CLIENTE:

| Metodo | Rota | Resultado |
|---|---|---|
| GET | /api/minhas-ordens | Somente as OS do cliente, mais novas primeiro |
| GET | /api/minhas-ordens/{id} | OS e historico, se pertence ao cliente |

A listagem inclui Finalizada, Entregue e Cancelada, pois nao e a fila operacional
do administrador. Sem OS, retorna lista vazia. ID alheio e ID inexistente recebem
404, sem confirmar a existencia da OS de outro cliente. O token e as respostas
autenticadas sao marcados como nao armazenaveis em cache.

O ID do dono vem exclusivamente do sub do JWT validado. Query, body e header
customizado nao escolhem outro cliente. O filtro ClienteId e aplicado no SQL,
sem carregar todas as OS para filtrar em memoria. Cada consulta exige cliente
ativo: desativar o cadastro bloqueia o token ja emitido com 403, sem esperar
expiracao e sem apagar historico. O token e vinculado ao ID, nao ao texto do CPF.

## Tokens e camadas

O token de cliente dura 30 minutos e possui sub=ClienteId, name, role=Cliente,
jti e metadados de tempo, issuer e audience. Nao inclui CPF. Usa HS256 com o
segredo ja configurado para Ordens, sem nova chave fixa no codigo.
JwtSettings:ClienteAudience tem o padrao OficinaMecanica.Ordens.Cliente e deve
ser diferente da Audience administrativa. Nenhum Secret do GitHub precisa ser
alterado nesta etapa local.

O esquema Cliente valida assinatura, algoritmo, emissor, audience e expiracao;
somente as duas rotas minhas-ordens o usam. Os endpoints administrativos usam
o esquema Bearer original e rejeitam o token de cliente. Execucao e Cobranca
tambem o rejeitam, mesmo compartilhando a chave local do Compose. O token
administrativo nao serve nas rotas exclusivas de cliente.

```text
Login: HTTP -> controller Clean -> LoginClienteUseCase
  -> gateways de cliente, documento e token -> sources EF/documento/JWT

Consulta: HTTP extrai sub validado -> controller Clean
  -> use case exige cliente ativo -> gateway -> source EF filtra ClienteId
  -> output -> presenter -> response DTO
```

Domain, Application e InterfaceAdapters continuam sem framework. ClaimsPrincipal,
autenticacao ASP.NET, rate limiter e validacoes HTTP ficam na Api; EF e JWT na
Infrastructure. Nao precisa de migration: nao criamos tabela de credenciais.

## Limite de tentativas

Somente POST /api/auth/cliente limita 10 tentativas por minuto por IP, sem fila.
Excesso recebe 429 e Retry-After. Nao usa CPF como chave. Login administrativo
e consultas nao compartilham essa politica. O contador e local a cada instancia,
nao global: antes de expor varias replicas, a etapa de gateway precisa tratar
limite compartilhado e encaminhamento de IP somente por proxies confiaveis.
O limitador nao torna CPF uma prova de identidade.

## Testar localmente

Na raiz de Ordens, com os tres repositorios lado a lado:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-acesso-cliente.py
```

O roteiro cria dois clientes e duas OS. Faz login com CPF simples e formatado;
testa consultas proprias, acesso cruzado, tentativa de escolher cliente pela
query, token adulterado e bloqueio administrativo nos tres servicos. Desativa
somente um cliente criado pelo teste e verifica a revogacao sem apagar a OS.
Nao exibe CPF ou tokens e nao altera registros preexistentes.

No Postman, cadastre cliente e OS com o token administrativo; faca login por
CPF e use tokenCliente somente em minhas-ordens. Nao substitua o token usado
para cadastrar, iniciar ou concluir reparos. Os testes automatizados verificam
tambem tokens expirados, sem assinatura, com audience/issuer/perfil/sub invalidos,
cadastro inativo, limite de tentativas e historico de OS encerradas.

Referencias oficiais:
[validacao JWT](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0),
[esquemas separados](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/authorize-with-a-specific-scheme?view=aspnetcore-10.0) e
[rate limiting por instancia](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).
