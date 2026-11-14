# Consulta e resposta do orcamento pelo cliente

## Fluxo entre os servicos

O cliente usa o token emitido por POST /api/auth/cliente e fala somente com
Ordens. Ordens exige cadastro ativo e verifica no seu banco se a OS pertence
ao cliente. Obtem da Saga os IDs e o valor do orcamento, nunca de um body do
usuario. Depois acessa Cobranca por HTTP autenticado, sem ler o banco dela.

```text
Cliente -> Ordens: controller HTTP -> controller Clean -> use case
  -> AcessoOrcamentoCliente verifica dono e vinculo da Saga
  -> gateway -> source HTTP -> endpoints internos de Cobranca
  -> controller HTTP -> controller Clean -> use case -> entidade Orcamento
  -> gateway -> source EF: decisao, historico e outbox na mesma transacao
  -> eventos OrcamentoAprovado/OrcamentoRecusado -> Saga existente em Ordens
```

Domain, Application e InterfaceAdapters continuam sem framework. HTTP e JWT
ficam na Infrastructure/Api; as interfaces do gateway/source so conhecem tipos
puros. Nao mudamos as mensagens da Saga, nao criamos banco compartilhado e nao
foi necessaria uma nova migration para este incremento.

O login somente por CPF continua sendo a simplificacao academica explicada no
[guia de acesso](11-acesso-cliente.md), nao autenticacao adequada para producao.
Este incremento nao envia email ou WhatsApp: disponibiliza o orcamento para
consulta e decisao autenticadas pela API.

## Endpoints publicos de Ordens

Todas as rotas abaixo exigem Authorization: Bearer TOKEN_DO_CLIENTE.
O {id} e o ID da OS, nao o do orcamento.

| Metodo | Rota | Body |
|---|---|---|
| GET | /api/minhas-ordens/{id}/orcamento | Nenhum |
| POST | /api/minhas-ordens/{id}/orcamento/aprovar | Nenhum, ou {} |
| POST | /api/minhas-ordens/{id}/orcamento/recusar | {"motivo":"Valor fora do meu orcamento"} |

Retornam 200 com ID do orcamento/OS/execucao, diagnostico, itens, precos, total,
status, data de criacao/resposta e motivo da recusa, quando houver. Nao retornam
credenciais internas. As respostas nao devem ser armazenadas em cache.

O cliente nao informa valor, status, IDs de vinculo ou identidade no body.
Campos extras sao rejeitados com 400. Recusa exige motivo nao vazio com ate
1000 caracteres, sem controles. Aprovacao nao recebe motivo nem valor.

- 401: token ausente, invalido ou administrativo em rota de cliente.
- 403: cliente desativado, inclusive com token ainda nao expirado.
- 404: OS inexistente, de outro cliente ou sem orcamento conhecido na Saga.
- 409: decisao contraditoria ou conflito de versao; consulte antes de repetir.
- 503: Cobranca indisponivel ou resultado remoto nao confirmado.

GET mostra o estado persistido em Cobranca. A Saga e a OS acompanham os eventos
assincronamente, portanto podem levar alguns instantes para refletir a decisao.
Uma aprovacao aceita leva ao pagamento mock e depois a fila de execucao, sem
iniciar o reparo. A recusa aciona a compensacao anterior: so cancela a OS depois
da confirmacao de Execucao, sem criar pagamento.

## Repeticao e resultado desconhecido

Repetir aprovacao devolve a aprovacao existente. Repetir recusa com o mesmo
motivo normalizado devolve a recusa existente. Nao duplica historico, evento
de decisao ou cobranca. Trocar a decisao ou o motivo de uma recusa ja gravada
recebe 409. A entidade de Cobranca continua sendo a unica autoridade da regra.

Se a conexao cair depois da gravacao, Ordens nao marca a decisao localmente nem
inventa uma confirmacao. Retorna 503, enquanto o evento duravel pode continuar
na Saga. Consulte novamente; repetir a mesma decisao e seguro. Nao existe
retentativa HTTP automatica de uma mutacao. Uma nova requisicao GET le o estado
persistido apos o POST, preservando a data de resposta nas repeticoes.

Ordens valida IDs, valor, itens, soma, status e datas antes de apresentar o
orcamento remoto. Cobranca valida OS, execucao, Saga, pedido original, valor e
correlacao antes de permitir a consulta ou a resposta interna. Um cliente de
outra OS e barrado antes de qualquer chamada remota.

## Autenticacao interna e configuracao

Ordens nao repassa o token do cliente e nao usa login de administrador para
chamar Cobranca. Emite um JWT de servico com duracao de 2 minutos, issuer
OficinaMecanica.Ordens.Integracao, audience OficinaMecanica.Cobranca.Orcamentos,
sub=ordens e role=OrcamentosCliente. Usa uma chave diferente da administrativa.
O X-Correlation-ID preserva a correlacao da Saga; nenhum CPF e enviado.

Cobranca aceita esse esquema somente em /internal/orcamentos/{id} e nos POSTs
/aprovar e /recusar dessa rota. Tokens de cliente/administrador nao servem ali,
e o token de servico nao serve nos endpoints administrativos ou financeiros.
Redirecionamentos HTTP nao sao seguidos pelo cliente interno.

A integracao fica desabilitada por padrao fora do Compose integrado. Para
configurar, Ordens usa OrcamentoCliente:Enabled, BaseUrl e SecretKey; Cobranca
usa OrdensAtendimento:Enabled e SecretKey. As chaves devem ser iguais entre
esses dois blocos, com pelo menos 32 bytes, e diferentes da chave JWT de admin.
O Compose configura tudo localmente e aceita override ORDENS_COBRANCA_SECRET.
A chave de exemplo e exclusiva de desenvolvimento e nao deve ser usada na AWS.

Antes do deploy da Fase 4, sera preciso injetar o segredo por Secrets, definir
a URL privada de Cobranca, proteger o transporte e impedir a exposicao de
/internal pelo gateway publico. Esta configuracao de laboratorio nao substitui
um provedor de identidade/credenciais de servico em um ambiente de producao.
Nenhum recurso AWS ou repositorio da Fase 3 foi alterado nesta etapa.

## Testar localmente

Na raiz de Ordens, com os tres repositorios lado a lado e Docker Desktop aberto:

```bash
docker compose -f compose.integracao.yml up --build -d
python3 infra/local/testar-orcamento-cliente.py
```

O roteiro cria dados novos. Um cliente aprova e outro recusa seus orcamentos;
um terceiro nao consegue consultar ou responder pelos outros. Verifica preco
preservado, repeticao, recusa de campos extras, contradicoes, uma unica cobranca
mock na aprovacao e ausencia de cobranca na recusa. Espera tambem o resultado
da Saga e confere que o reparo nao inicia sozinho. Nao exibe tokens nem CPF.

Os testes automatizados adicionais verificam indisponibilidade, JSON invalido,
resposta de outra OS, valor inconsistente, falha apos gravacao, assinatura,
issuer, audience, sub e perfil do servico, alem da integracao desabilitada.
Para simular indisponibilidade manualmente, pare apenas Cobranca depois de
gerar o orcamento, chame GET do cliente (503), reinicie Cobranca e consulte de
novo. Nao pare ou apague bancos para esse teste.

Referencias: [esquemas de autenticacao separados](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/authorize-with-a-specific-scheme?view=aspnetcore-10.0)
e [validacao JWT](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
