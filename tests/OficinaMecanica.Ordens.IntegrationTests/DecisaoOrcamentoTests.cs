using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class DecisaoOrcamentoTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DecisaoAntesDaConfirmacao_DeveAplicarSemDuplicarOuRegredir(bool aprovado)
    {
        using var factory = await Criar(); var decisao = Decisao(factory, aprovado);
        Processar(factory, decisao); Processar(factory, decisao);
        var gerado = new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "OrcamentoGerado",
            SagaId = decisao.SagaId,
            CausationId = decisao.CausationId,
            CorrelationId = decisao.CorrelationId,
            OccurredAt = decisao.Payload.OrcamentoCriadoEm!.Value,
            Payload = new DadosMensagem
            {
                OrdemServicoId = decisao.Payload.OrdemServicoId,
                ExecucaoId = decisao.Payload.ExecucaoId,
                OrcamentoId = decisao.Payload.OrcamentoId,
                ValorTotal = decisao.Payload.ValorTotal
            }
        };
        Processar(factory, gerado);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single();
        Assert.Equal(aprovado ? StatusSagaAbertura.AguardandoPagamento : StatusSagaAbertura.CancelamentoPorRecusaPendente, saga.Status);
        Assert.Equal(aprovado, saga.OrcamentoAprovado);
        Assert.Equal(aprovado ? StatusOrdemServico.AguardandoPagamento : StatusOrdemServico.AguardandoAprovacao, db.OrdensServico.Single().Status);
        Assert.Equal(aprovado ? 4 : 3, db.HistoricoStatus.Count());
        Assert.Equal(aprovado ? 1 : 2, db.Outbox.Count());
        Assert.Equal(2, db.Inbox.Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recusa_SoCancelaOSAposConfirmacaoDaExecucao(bool aceito)
    {
        using var factory = await Criar(); var decisao = Decisao(factory, false); Processar(factory, decisao);
        var resposta = Cancelamento(factory, aceito);
        Processar(factory, resposta); Processar(factory, resposta);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(aceito ? StatusSagaAbertura.OrcamentoRecusado : StatusSagaAbertura.FalhaRequerIntervencao, db.SagasAbertura.Single().Status);
        Assert.Equal(aceito ? StatusOrdemServico.Cancelada : StatusOrdemServico.AguardandoAprovacao, db.OrdensServico.Single().Status);
        Assert.Equal(aceito ? 4 : 3, db.HistoricoStatus.Count());
        Assert.Equal(2, db.Inbox.Count()); Assert.Equal(2, db.Outbox.Count());
    }

    [Theory]
    [InlineData("valor")]
    [InlineData("causa")]
    [InlineData("execucao")]
    [InlineData("correlacao")]
    public async Task DecisaoInvalida_NaoGravaInboxOuTransicao(string erro)
    {
        using var factory = await Criar(); var decisao = Decisao(factory, true);
        if (erro == "valor") decisao.Payload.ValorTotal = 0;
        if (erro == "causa") decisao.CausationId = Guid.NewGuid();
        if (erro == "execucao") decisao.Payload.ExecucaoId = Guid.NewGuid();
        if (erro == "correlacao") decisao.CorrelationId = "outra";
        Assert.Throws<ValidacaoException>(() => Processar(factory, decisao));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.Inbox); Assert.Single(db.Outbox);
        Assert.Equal(StatusSagaAbertura.OrcamentoPendente, db.SagasAbertura.Single().Status);
    }

    [Fact]
    public async Task RespostaContraditoria_NaoPodeTrocarAprovacaoPorRecusa()
    {
        using var factory = await Criar(); var decisao = Decisao(factory, true); Processar(factory, decisao);
        decisao.MessageId = Guid.NewGuid(); decisao.Type = "OrcamentoRecusado"; decisao.Payload.Motivo = "Outra resposta";
        Assert.Throws<InvalidOperationException>(() => Processar(factory, decisao));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(db.Inbox); Assert.Single(db.Outbox);
        Assert.Equal(StatusSagaAbertura.AguardandoPagamento, db.SagasAbertura.Single().Status);
    }

    [Fact]
    public async Task ConfirmacaoDeCancelamentoComCausaErrada_NaoCancelaOS()
    {
        using var factory = await Criar(); Processar(factory, Decisao(factory, false));
        var resposta = Cancelamento(factory, true); resposta.CausationId = Guid.NewGuid();
        Assert.Throws<ValidacaoException>(() => Processar(factory, resposta));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, scope.ServiceProvider.GetRequiredService<AppDbContext>().OrdensServico.Single().Status);
    }

    [Fact]
    public async Task DecisoesConcorrentes_NaoGravamCompensacaoOuInboxDaRespostaPerdedora()
    {
        using var factory = await Criar();
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sagaA = dbA.SagasAbertura.Single(); var sagaB = dbB.SagasAbertura.Single();
        var orcamento = Guid.NewGuid(); var agora = DateTime.UtcNow; var eventoA = Guid.NewGuid(); var eventoB = Guid.NewGuid();
        sagaA.ConfirmarOrcamento(orcamento, 100m); sagaB.ConfirmarOrcamento(orcamento, 100m);
        sagaA.ResponderOrcamento(eventoA, true, agora, agora, null);
        sagaB.ResponderOrcamento(eventoB, false, agora, agora, "Valor alto");
        new EfSagaAberturaDataSource(dbA).Salvar(sagaA, eventoA, null, false, StatusOrdemServico.AguardandoPagamento);
        var comando = new MensagemIntegracao
        {
            MessageId = sagaB.PedidoCancelamentoPorRecusaId!.Value,
            Type = "CancelarExecucaoPorRecusaOrcamento",
            SagaId = sagaB.Id,
            CausationId = eventoB,
            OccurredAt = agora,
            CorrelationId = sagaB.CorrelationId,
            Payload = new DadosMensagem
            {
                OrdemServicoId = sagaB.OrdemServicoId,
                ExecucaoId = sagaB.ExecucaoId,
                OrcamentoId = orcamento,
                Motivo = "Valor alto"
            }
        };
        Assert.Throws<DbUpdateConcurrencyException>(() => new EfSagaAberturaDataSource(dbB)
            .Salvar(sagaB, eventoB, comando, false, StatusOrdemServico.AguardandoAprovacao, agora));
        using var verificar = factory.Services.CreateScope(); var banco = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(banco.Inbox); Assert.Single(banco.Outbox);
        Assert.Equal(StatusSagaAbertura.AguardandoPagamento, banco.SagasAbertura.Single().Status);
        Assert.Equal(StatusOrdemServico.AguardandoPagamento, banco.OrdensServico.Single().Status);
    }

    private static async Task<OrdensApiFactory> Criar()
    {
        var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single(); var inicio = DateTime.UtcNow.AddMinutes(-1);
        saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), inicio, inicio.AddSeconds(1));
        new EfSagaAberturaDataSource(db).Salvar(saga, null, null, false, StatusOrdemServico.EmDiagnostico);
        return factory;
    }

    private static MensagemIntegracao Decisao(OrdensApiFactory factory, bool aprovado)
    {
        using var scope = factory.Services.CreateScope(); var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.AsNoTracking().Single();
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = aprovado ? "OrcamentoAprovado" : "OrcamentoRecusado",
            SagaId = saga.Id,
            CausationId = saga.PedidoOrcamentoId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = Guid.NewGuid(),
                ValorTotal = 100m,
                OrcamentoCriadoEm = DateTime.UtcNow.AddSeconds(-1),
                Motivo = aprovado ? null : "Valor alto"
            }
        };
    }

    private static MensagemIntegracao Cancelamento(OrdensApiFactory factory, bool aceito)
    {
        using var scope = factory.Services.CreateScope(); var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.AsNoTracking().Single();
        var comando = MensagemSerializer.Desserializar(scope.ServiceProvider.GetRequiredService<AppDbContext>().Outbox.Single(item => item.Id == saga.PedidoCancelamentoPorRecusaId).Body);
        Assert.Equal(saga.EventoRespostaOrcamentoId, comando.CausationId);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = aceito ? "ExecucaoCanceladaPorRecusaOrcamento" : "CancelamentoPorRecusaOrcamentoRecusado",
            SagaId = saga.Id,
            CausationId = comando.MessageId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = saga.OrcamentoId,
                Codigo = aceito ? null : "ExecucaoNaoCancelavel",
                Motivo = aceito ? null : "Trabalho ja iniciado"
            }
        };
    }

    private static void Processar(OrdensApiFactory factory, MensagemIntegracao mensagem)
    {
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(mensagem);
    }
}
