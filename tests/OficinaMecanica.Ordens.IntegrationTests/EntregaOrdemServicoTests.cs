using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Gateways;
using static OficinaMecanica.Ordens.IntegrationTests.LiberacaoExecucaoTests;
using static OficinaMecanica.Ordens.IntegrationTests.InicioReparoTests;
using static OficinaMecanica.Ordens.IntegrationTests.ConclusaoReparoTests;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class EntregaOrdemServicoTests
{
    [Fact]
    public async Task Entrega_PreservaHistoricoENaoAlteraSagaOuGeraMensagens()
    {
        var fixture = await Finalizada(); using var factory = fixture.Factory;
        using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var id = fixture.Conclusao.Payload.OrdemServicoId;
        OrdemServicoResponseDto? primeira = null;
        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            var resposta = await client.PostAsync($"/api/ordensservico/{id}/entregar", null);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var entregue = await resposta.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json);
            Assert.Equal(StatusOrdemServico.Entregue, entregue!.Status); Assert.Equal(9, entregue.Historico.Count);
            Assert.Equal(StatusOrdemServico.Entregue, entregue.Historico[^1].Status);
            primeira ??= entregue;
            Assert.Equal(primeira.Historico[^1].RegistradoEm, entregue.Historico[^1].RegistradoEm);
        }
        var consulta = await client.GetFromJsonAsync<OrdemServicoResponseDto>($"/api/ordensservico/{id}", ApiTestHelper.Json);
        Assert.Equal(StatusOrdemServico.Entregue, consulta!.Status);
        Assert.Equal(primeira!.Historico[^1].RegistradoEm, consulta.Historico[^1].RegistradoEm);
        var fila = await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>("/api/ordensservico", ApiTestHelper.Json);
        Assert.DoesNotContain(fila!, item => item.Id == id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(9, db.HistoricoStatus.Count()); Assert.Equal(1, db.HistoricoStatus.Count(item => item.Status == StatusOrdemServico.Entregue));
        Assert.Equal(StatusSagaAbertura.Finalizada, db.SagasAbertura.Single().Status);
        Assert.Equal(4, db.Inbox.Count()); Assert.Equal(2, db.Outbox.Count());
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    [InlineData(StatusOrdemServico.AguardandoPagamento)]
    [InlineData(StatusOrdemServico.PagamentoConfirmado)]
    [InlineData(StatusOrdemServico.AguardandoExecucao)]
    [InlineData(StatusOrdemServico.CancelamentoPendente)]
    public async Task AntesDeFinalizar_NaoEntrega(StatusOrdemServico status)
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = db.OrdensServico.Select(item => item.Id).Single();
        db.OrdensServico.Where(item => item.Id == id).ExecuteUpdate(update => update.SetProperty(item => item.Status, status));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/ordensservico/{id}/entregar", null)).StatusCode);
        Assert.Equal(status, db.OrdensServico.AsNoTracking().Single().Status); Assert.Single(db.HistoricoStatus);
    }

    [Fact]
    public async Task SemLoginOuIdInexistente_NaoEntrega()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/ordensservico/{Guid.NewGuid()}/entregar", null)).StatusCode);
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/ordensservico/{Guid.NewGuid()}/entregar", null)).StatusCode);
    }

    [Fact]
    public async Task DuasCopiasDaOS_ApenasUmaConfirmaEntrega()
    {
        var fixture = await Finalizada(); using var factory = fixture.Factory;
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sourceA = new EfOrdemServicoDataSource(dbA); var sourceB = new EfOrdemServicoDataSource(dbB);
        var id = fixture.Conclusao.Payload.OrdemServicoId;
        var primeira = sourceA.ObterPorId(id)!; var segunda = sourceB.ObterPorId(id)!;
        primeira.Entregar(); segunda.Entregar();
        Assert.True(sourceA.TentarRegistrarEntrega(primeira)); Assert.False(sourceB.TentarRegistrarEntrega(segunda));
        var repetida = new EntregarOrdemServicoUseCase(new OrdemServicoGateway(sourceB)).Executar(id);
        Assert.Equal(StatusOrdemServico.Entregue, repetida!.Status); Assert.Equal(9, repetida.Historico.Count);
        Assert.Equal(1, dbB.HistoricoStatus.Count(item => item.Status == StatusOrdemServico.Entregue));
    }

    [Fact]
    public async Task FalhaAoSalvarHistorico_DesfazMudancaDeStatus()
    {
        var fixture = await Finalizada(); using var factory = fixture.Factory;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var source = new EfOrdemServicoDataSource(db);
            var ordem = source.ObterPorId(fixture.Conclusao.Payload.OrdemServicoId)!;
            var idExistente = ordem.Historico.First().Id; ordem.Entregar();
            ordem.Historico.Single(item => item.Status == StatusOrdemServico.Entregue).Id = idExistente;
            Assert.Throws<DbUpdateException>(() => source.TentarRegistrarEntrega(ordem));
        }
        using var verificar = factory.Services.CreateScope(); var salvo = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusOrdemServico.Finalizada, salvo.OrdensServico.AsNoTracking().Single().Status);
        Assert.Equal(8, salvo.HistoricoStatus.Count()); Assert.False(salvo.HistoricoStatus.Any(item => item.Status == StatusOrdemServico.Entregue));
    }

    [Fact]
    public async Task ConclusaoAtrasada_NaoReabreOSJaEntregue()
    {
        var fixture = await Finalizada(); using var factory = fixture.Factory;
        using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var id = fixture.Conclusao.Payload.OrdemServicoId;
        (await client.PostAsync($"/api/ordensservico/{id}/entregar", null)).EnsureSuccessStatusCode();
        fixture.Conclusao.MessageId = Guid.NewGuid(); Processar(factory, fixture.Conclusao);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusOrdemServico.Entregue, db.OrdensServico.AsNoTracking().Single().Status);
        Assert.Equal(9, db.HistoricoStatus.Count()); Assert.Equal(StatusSagaAbertura.Finalizada, db.SagasAbertura.Single().Status);
    }

    private static async Task<(OrdensApiFactory Factory, MensagemIntegracao Conclusao)> Finalizada()
    {
        var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        var conclusao = Conclusao(inicio);
        conclusao.OccurredAt = inicio.OccurredAt; conclusao.Payload.ReparoConcluidoEm = conclusao.OccurredAt;
        Processar(factory, liberacao); Processar(factory, inicio); Processar(factory, conclusao);
        return (factory, conclusao);
    }
}
