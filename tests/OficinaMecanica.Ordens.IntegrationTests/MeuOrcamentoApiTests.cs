using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class MeuOrcamentoApiTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cliente_ConsultaERespondeSemAlterarSagaOuCriarPagamentoLocal(bool aprovado)
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler); var tokenCliente = client.DefaultRequestHeaders.Authorization!.Parameter;
        var rota = $"/api/minhas-ordens/{ordem.Id}/orcamento";
        var original = await client.GetFromJsonAsync<OrcamentoClienteResponseDto>(rota);
        Assert.Equal(200m, original!.ValorTotal); Assert.Equal(2, original.Itens.Count); Assert.Equal("AguardandoAprovacao", original.Status);
        HttpResponseMessage response = aprovado ? await client.PostAsync(rota + "/aprovar", null) : await client.PostAsJsonAsync(rota + "/recusar", new { motivo = "  Valor alto  " });
        response.EnsureSuccessStatusCode(); var final = (await response.Content.ReadFromJsonAsync<OrcamentoClienteResponseDto>())!;
        Assert.Equal(aprovado ? "Aprovado" : "Recusado", final.Status); Assert.Equal(aprovado ? null : "Valor alto", final.MotivoRecusa);
        var repetida = aprovado ? await client.PostAsync(rota + "/aprovar", null) : await client.PostAsJsonAsync(rota + "/recusar", new { motivo = "Valor alto" });
        repetida.EnsureSuccessStatusCode(); Assert.Equal(final.RespondidoEm, (await repetida.Content.ReadFromJsonAsync<OrcamentoClienteResponseDto>())!.RespondidoEm);
        Assert.Equal(1, handler.Decisoes); Assert.NotEqual(tokenCliente, handler.UltimoToken);
        Assert.Equal(ordem.Id, handler.UltimoBody.GetProperty("vinculo").GetProperty("ordemServicoId").GetGuid());
        Assert.False(handler.UltimoBody.GetProperty("vinculo").TryGetProperty("clienteId", out _));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.OrcamentoGerado, db.SagasAbertura.Single().Status); Assert.Single(db.Outbox);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, db.OrdensServico.Single().Status);
    }
    [Fact]
    public async Task OrdemAlheia_NaoConsultaNemRespondeCobranca()
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler); using var outro = factory.CreateClient(); await ApiTestHelper.Autenticar(outro);
        (await outro.PostAsJsonAsync("/api/clientes", ApiTestHelper.Cliente("11144477735"))).EnsureSuccessStatusCode();
        var login = await outro.PostAsJsonAsync("/api/auth/cliente", new { cpf = "11144477735" }); login.EnsureSuccessStatusCode();
        outro.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginClienteResponseDto>())!.Token);
        var rota = $"/api/minhas-ordens/{ordem.Id}/orcamento";
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync(rota)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.PostAsync(rota + "/aprovar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.PostAsJsonAsync(rota + "/recusar", new { motivo = "Outro cliente" })).StatusCode);
        Assert.Equal(0, handler.Chamadas);
    }
    [Fact]
    public async Task SemLoginOuAdmin_ClienteNaoEscolheValorStatusOuVinculoNoBody()
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler); var rota = $"/api/minhas-ordens/{ordem.Id}/orcamento";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(rota + "/aprovar", new { valorTotal = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(rota + "/recusar", new { motivo = "Teste", clienteId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(rota + "/recusar", new { motivo = "\n" })).StatusCode);
        using var anonimo = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(rota)).StatusCode);
        await ApiTestHelper.Autenticar(anonimo); Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync(rota + "/aprovar", null)).StatusCode);
        Assert.Equal(0, handler.Chamadas);
    }
    [Theory]
    [InlineData("indisponivel")]
    [InlineData("json")]
    [InlineData("os")]
    [InlineData("total")]
    public async Task RemotoIndisponivelOuDivergente_NaoFingeSucesso(string erro)
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler);
        if (erro == "os") handler.Orcamento.OrdemServicoId = Guid.NewGuid(); else if (erro == "total") handler.Orcamento.Itens[0].Total = 1m; else handler.Modo = erro;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync($"/api/minhas-ordens/{ordem.Id}/orcamento")).StatusCode);
        Assert.Equal(0, handler.Decisoes);
    }
    [Fact]
    public async Task FalhaDepoisDaGravacao_RetentativaConfirmaSemOutraDecisao()
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler); handler.Modo = "timeout-depois-gravar"; var rota = $"/api/minhas-ordens/{ordem.Id}/orcamento";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(rota + "/aprovar", null)).StatusCode);
        (await client.PostAsync(rota + "/aprovar", null)).EnsureSuccessStatusCode(); Assert.Equal(1, handler.Decisoes);
        Assert.Equal("Aprovado", (await client.GetFromJsonAsync<OrcamentoClienteResponseDto>(rota))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(rota + "/recusar", new { motivo = "Contradicao" })).StatusCode);
    }
    [Fact]
    public async Task DesativacaoOuSemOrcamento_BloqueiaAntesDaRede()
    {
        using var handler = new OrcamentoRemotoHandler(); using var factory = new OrcamentoClienteApiFactory(handler); using var client = factory.CreateClient();
        var ordem = await Criar(factory, client, handler); var rota = $"/api/minhas-ordens/{ordem.Id}/orcamento";
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.SagasAbertura.Single().OrcamentoId = null; db.SaveChanges(); }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(rota)).StatusCode);
        using var admin = factory.CreateClient(); await ApiTestHelper.Autenticar(admin); (await admin.DeleteAsync($"/api/clientes/{ordem.ClienteId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(rota)).StatusCode); Assert.Equal(0, handler.Chamadas);
    }
    private static async Task<OrdemServicoResponseDto> Criar(OrdensApiFactory factory, HttpClient client, OrcamentoRemotoHandler handler)
    {
        await ApiTestHelper.Autenticar(client); var created = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta()); created.EnsureSuccessStatusCode();
        var ordem = (await created.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var saga = db.SagasAbertura.Single();
            var inicio = DateTime.UtcNow.AddMinutes(-1); var criado = DateTime.UtcNow;
            saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), inicio, inicio.AddSeconds(1)); saga.ConfirmarOrcamento(Guid.NewGuid(), 200m);
            db.OrdensServico.Include(item => item.Historico).Single().AguardarAprovacao(inicio, criado); db.SaveChanges();
            handler.Orcamento = new OrcamentoClienteOutput
            {
                Id = saga.OrcamentoId!.Value,
                OrdemServicoId = ordem.Id,
                ExecucaoId = saga.ExecucaoId!.Value,
                Status = "AguardandoAprovacao",
                ValorTotal = 200m,
                CriadoEm = criado,
                Diagnostico = "Trocar filtro",
                Itens = [new() { Tipo = "Servico", ReferenciaId = Guid.NewGuid(), Nome = "Revisao", Quantidade = 1, PrecoUnitario = 100m, Total = 100m },
                    new() { Tipo = "PecaInsumo", ReferenciaId = Guid.NewGuid(), Nome = "Filtro", Quantidade = 2, PrecoUnitario = 50m, Total = 100m }]
            };
        }
        var login = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725" }); login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginClienteResponseDto>())!.Token);
        return ordem;
    }
}
