using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using static OficinaMecanica.Ordens.IntegrationTests.LiberacaoExecucaoTests;
using static OficinaMecanica.Ordens.IntegrationTests.InicioReparoTests;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class ConclusaoReparoTests
{
    [Fact]
    public async Task Conclusao_FinalizaUmaVezESaiDaListagemSemApagarHistorico()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Processar(factory, liberacao); Processar(factory, inicio); var conclusao = Conclusao(inicio);
        Processar(factory, conclusao); Processar(factory, conclusao);
        conclusao.MessageId = Guid.NewGuid(); Processar(factory, conclusao);
        inicio.MessageId = Guid.NewGuid(); Processar(factory, inicio);
        liberacao.MessageId = Guid.NewGuid(); Processar(factory, liberacao);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.Finalizada, saga.Status); Assert.Equal(conclusao.OccurredAt, saga.ReparoConcluidoEm);
        Assert.Equal(StatusOrdemServico.Finalizada, db.OrdensServico.Single().Status);
        Assert.Equal(8, db.HistoricoStatus.Count()); Assert.Equal(2, db.Outbox.Count()); Assert.NotNull(saga.PagamentoConfirmadoEm);
        using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var fila = await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>("/api/ordensservico", ApiTestHelper.Json);
        Assert.DoesNotContain(fila!, item => item.Id == saga.OrdemServicoId);
        var ordem = await client.GetFromJsonAsync<OrdemServicoResponseDto>($"/api/ordensservico/{saga.OrdemServicoId}", ApiTestHelper.Json);
        Assert.Equal(StatusOrdemServico.Finalizada, ordem!.Status);
    }

    [Fact]
    public async Task ConclusaoAntesDoInicio_NaoConfirmaMensagemEPodeSerReentregue()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao); var conclusao = Conclusao(inicio);
        Assert.Throws<InvalidOperationException>(() => Processar(factory, conclusao));
        Processar(factory, liberacao);
        Assert.Throws<InvalidOperationException>(() => Processar(factory, conclusao));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(2, db.Inbox.Count());
            Assert.Equal(StatusOrdemServico.AguardandoExecucao, db.OrdensServico.Single().Status);
        }
        Processar(factory, inicio); Processar(factory, conclusao);
        using var verificar = factory.Services.CreateScope();
        Assert.Equal(StatusSagaAbertura.Finalizada, verificar.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.Single().Status);
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("pagamento")]
    [InlineData("valor")]
    [InlineData("correlacao")]
    [InlineData("inicio")]
    [InlineData("conclusao")]
    [InlineData("fuso")]
    public async Task ConclusaoDivergente_NaoFinalizaNemGravaInbox(string erro)
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Processar(factory, liberacao); Processar(factory, inicio); var conclusao = Conclusao(inicio);
        if (erro == "causa") conclusao.CausationId = Guid.NewGuid();
        if (erro == "pagamento") conclusao.Payload.PagamentoId = Guid.NewGuid();
        if (erro == "valor") conclusao.Payload.ValorTotal = 1m;
        if (erro == "correlacao") conclusao.CorrelationId = "outra";
        if (erro == "inicio") conclusao.Payload.ReparoIniciadoEm = inicio.OccurredAt.AddSeconds(-1);
        if (erro == "conclusao") conclusao.Payload.ReparoConcluidoEm = conclusao.OccurredAt.AddSeconds(1);
        if (erro == "fuso") conclusao.Payload.ReparoConcluidoEm = DateTime.SpecifyKind(conclusao.OccurredAt, DateTimeKind.Unspecified);
        Assert.Throws<ValidacaoException>(() => Processar(factory, conclusao));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, db.Inbox.Count()); Assert.Equal(StatusOrdemServico.EmExecucao, db.OrdensServico.Single().Status);
    }

    [Fact]
    public async Task ConclusaoContraditoria_NaoTrocaDataFinal()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Processar(factory, liberacao); Processar(factory, inicio); var conclusao = Conclusao(inicio); Processar(factory, conclusao);
        var data = conclusao.OccurredAt; conclusao.MessageId = Guid.NewGuid(); conclusao.OccurredAt = data.AddSeconds(1);
        conclusao.Payload.ReparoConcluidoEm = conclusao.OccurredAt;
        Assert.Throws<InvalidOperationException>(() => Processar(factory, conclusao));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(data, db.SagasAbertura.Single().ReparoConcluidoEm); Assert.Equal(4, db.Inbox.Count()); Assert.Equal(8, db.HistoricoStatus.Count());
    }

    [Fact]
    public async Task ConclusoesConcorrentes_UmaVersaoPerdedoraNaoGravaInboxOuHistorico()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Processar(factory, liberacao); Processar(factory, inicio);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sagaA = dbA.SagasAbertura.Single(); var sagaB = dbB.SagasAbertura.Single(); var agora = inicio.OccurredAt.AddSeconds(1);
        sagaA.RegistrarConclusaoReparo(inicio.OccurredAt, agora); sagaB.RegistrarConclusaoReparo(inicio.OccurredAt, agora);
        new EfSagaAberturaDataSource(dbA).Salvar(sagaA, Guid.NewGuid(), null, false, StatusOrdemServico.Finalizada, agora);
        Assert.Throws<DbUpdateConcurrencyException>(() => new EfSagaAberturaDataSource(dbB).Salvar(sagaB, Guid.NewGuid(), null, false, StatusOrdemServico.Finalizada, agora));
        using var verificar = factory.Services.CreateScope(); var db = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.Finalizada, db.SagasAbertura.Single().Status);
        Assert.Equal(4, db.Inbox.Count()); Assert.Equal(8, db.HistoricoStatus.Count());
    }

    private static MensagemIntegracao Conclusao(MensagemIntegracao inicio)
    {
        var concluido = inicio.OccurredAt.AddSeconds(1);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "ReparoConcluido",
            SagaId = inicio.SagaId,
            CausationId = inicio.CausationId,
            CorrelationId = inicio.CorrelationId,
            OccurredAt = concluido,
            Payload = new DadosMensagem
            {
                OrdemServicoId = inicio.Payload.OrdemServicoId,
                ExecucaoId = inicio.Payload.ExecucaoId,
                OrcamentoId = inicio.Payload.OrcamentoId,
                PagamentoId = inicio.Payload.PagamentoId,
                ProvedorId = inicio.Payload.ProvedorId,
                ValorTotal = inicio.Payload.ValorTotal,
                Moeda = "BRL",
                ReparoIniciadoEm = inicio.OccurredAt,
                ReparoConcluidoEm = concluido
            }
        };
    }
}
