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

public class ResultadoPagamentoTests
{
    [Theory]
    [InlineData("PagamentoConfirmado")]
    [InlineData("PagamentoRecusado")]
    [InlineData("FalhaProcessamentoPagamento")]
    public async Task Resultado_SolicitaLiberacaoSomenteSePagamentoConfirmado(string tipo)
    {
        using var factory = await Criar(); var resultado = Resultado(factory, tipo);
        Processar(factory, resultado); Processar(factory, resultado);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var confirmado = tipo == "PagamentoConfirmado"; var saga = db.SagasAbertura.Single();
        Assert.Equal(confirmado ? StatusSagaAbertura.LiberacaoExecucaoPendente : StatusSagaAbertura.FalhaRequerIntervencao, saga.Status);
        Assert.Equal(resultado.Payload.PagamentoId, saga.PagamentoId);
        Assert.Equal(confirmado ? StatusOrdemServico.PagamentoConfirmado : StatusOrdemServico.AguardandoPagamento, db.OrdensServico.Single().Status);
        Assert.Equal(confirmado ? 5 : 4, db.HistoricoStatus.Count()); Assert.Equal(2, db.Inbox.Count());
        Assert.Equal(confirmado ? 3 : 2, db.Outbox.Count());
        var liberacoes = db.Outbox.AsEnumerable().Select(item => MensagemSerializer.Desserializar(item.Body)).Where(item => item.Type == "LiberarExecucao").ToList();
        if (confirmado)
        {
            var comando = Assert.Single(liberacoes);
            Assert.Equal(resultado.MessageId, comando.CausationId);
            Assert.Equal(saga.PedidoLiberacaoExecucaoId, comando.MessageId);
            Assert.Equal(saga.PagamentoId, comando.Payload.PagamentoId);
        }
        else Assert.Empty(liberacoes);
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("valor")]
    [InlineData("moeda")]
    [InlineData("orcamento")]
    [InlineData("provedor")]
    public async Task ResultadoDivergente_NaoConfirmaPagamento(string erro)
    {
        using var factory = await Criar(); var resultado = Resultado(factory, "PagamentoConfirmado");
        if (erro == "causa") resultado.CausationId = Guid.NewGuid();
        if (erro == "valor") resultado.Payload.ValorTotal = 101m;
        if (erro == "moeda") resultado.Payload.Moeda = "USD";
        if (erro == "orcamento") resultado.Payload.OrcamentoId = Guid.NewGuid();
        if (erro == "provedor") resultado.Payload.ProvedorId = null;
        Assert.Throws<ValidacaoException>(() => Processar(factory, resultado));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(db.Inbox); Assert.Equal(StatusOrdemServico.AguardandoPagamento, db.OrdensServico.Single().Status);
    }

    [Fact]
    public async Task RecusaAposConfirmacao_NaoRegridePagamentoConfirmado()
    {
        using var factory = await Criar(); var resultado = Resultado(factory, "PagamentoConfirmado"); Processar(factory, resultado);
        resultado.MessageId = Guid.NewGuid(); resultado.Type = "PagamentoRecusado";
        resultado.Payload.Codigo = "PagamentoRecusado"; resultado.Payload.Motivo = "Resposta contraditoria";
        Assert.Throws<InvalidOperationException>(() => Processar(factory, resultado));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.LiberacaoExecucaoPendente, db.SagasAbertura.Single().Status); Assert.Equal(2, db.Inbox.Count());
    }

    [Fact]
    public async Task RespostasConcorrentes_NaoGravamFalhaDepoisDaConfirmacao()
    {
        using var factory = await Criar(); using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sagaA = dbA.SagasAbertura.Single(); var sagaB = dbB.SagasAbertura.Single(); var id = Guid.NewGuid();
        sagaA.RegistrarResultadoPagamento(id, "mock-id", true, DateTime.UtcNow, null);
        sagaB.RegistrarResultadoPagamento(id, null, false, DateTime.UtcNow, "Desconhecido");
        new EfSagaAberturaDataSource(dbA).Salvar(sagaA, Guid.NewGuid(), null, false, StatusOrdemServico.PagamentoConfirmado, DateTime.UtcNow);
        Assert.Throws<DbUpdateConcurrencyException>(() => new EfSagaAberturaDataSource(dbB).Salvar(sagaB, Guid.NewGuid(), null, false));
        using var verificar = factory.Services.CreateScope(); var banco = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusOrdemServico.PagamentoConfirmado, banco.OrdensServico.Single().Status); Assert.Equal(2, banco.Inbox.Count());
    }

    [Theory]
    [InlineData("PagamentoConfirmado")]
    [InlineData("PagamentoRecusado")]
    public async Task Reconciliacao_AceitaResultadoConhecidoSemRepetirLiberacaoOuRegredirPorFalhaAntiga(string tipo)
    {
        using var factory = await Criar(); var falha = Resultado(factory, "FalhaProcessamentoPagamento");
        Processar(factory, falha);
        var resultado = Resultado(factory, tipo); resultado.Payload.PagamentoId = falha.Payload.PagamentoId;
        Processar(factory, resultado); Processar(factory, resultado);
        falha.MessageId = Guid.NewGuid(); Processar(factory, falha);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single();
        Assert.Equal(falha.Payload.PagamentoId, saga.PagamentoId); Assert.Equal("mock-id", saga.PagamentoProvedorId);
        Assert.Equal(tipo == "PagamentoConfirmado" ? StatusSagaAbertura.LiberacaoExecucaoPendente : StatusSagaAbertura.FalhaRequerIntervencao, saga.Status);
        Assert.Equal(tipo == "PagamentoConfirmado" ? 3 : 2, db.Outbox.Count());
        if (tipo == "PagamentoConfirmado") Assert.Null(saga.Motivo);
    }

    [Fact]
    public async Task Reconciliacao_NaoSubstituiPagamentoRecusadoConhecidoNemTrocaIdentificador()
    {
        using var factory = await Criar(); var recusa = Resultado(factory, "PagamentoRecusado"); Processar(factory, recusa);
        var confirmacao = Resultado(factory, "PagamentoConfirmado"); confirmacao.Payload.PagamentoId = recusa.Payload.PagamentoId;
        Assert.Throws<InvalidOperationException>(() => Processar(factory, confirmacao));
        confirmacao.Payload.PagamentoId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Processar(factory, confirmacao));
    }

    private static async Task<OrdensApiFactory> Criar()
    {
        var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        Guid sagaId; Guid pedidoId; Guid ordemId; Guid execucaoId; Guid orcamentoId; string correlacao;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var saga = db.SagasAbertura.Single();
            var inicio = DateTime.UtcNow.AddMinutes(-1); execucaoId = Guid.NewGuid(); orcamentoId = Guid.NewGuid();
            saga.SolicitarOrcamento(Guid.NewGuid(), execucaoId, inicio, inicio.AddSeconds(1)); saga.ConfirmarOrcamento(orcamentoId, 100m);
            new EfSagaAberturaDataSource(db).Salvar(saga, null, null, false, StatusOrdemServico.AguardandoAprovacao, DateTime.UtcNow.AddSeconds(-2));
            sagaId = saga.Id; pedidoId = saga.PedidoOrcamentoId!.Value; ordemId = saga.OrdemServicoId;
            correlacao = saga.CorrelationId;
        }
        Processar(factory, new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "OrcamentoAprovado",
            SagaId = sagaId,
            CausationId = pedidoId,
            CorrelationId = correlacao,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = ordemId,
                ExecucaoId = execucaoId,
                OrcamentoId = orcamentoId,
                ValorTotal = 100m,
                OrcamentoCriadoEm = DateTime.UtcNow.AddSeconds(-2)
            }
        });
        return factory;
    }

    private static MensagemIntegracao Resultado(OrdensApiFactory factory, string tipo)
    {
        using var scope = factory.Services.CreateScope(); var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.AsNoTracking().Single();
        var sucesso = tipo == "PagamentoConfirmado"; var falha = tipo == "FalhaProcessamentoPagamento";
        var comando = new EfOutboxDataSource(scope.ServiceProvider.GetRequiredService<AppDbContext>()).ObterPendentes(10).Single(item => item.Type == "SolicitarPagamento");
        Assert.Equal(saga.EventoRespostaOrcamentoId, comando.CausationId);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = tipo,
            SagaId = saga.Id,
            CausationId = saga.PedidoPagamentoId,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = saga.CorrelationId,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = saga.OrcamentoId,
                ValorTotal = 100m,
                Moeda = "BRL",
                PagamentoId = Guid.NewGuid(),
                ProvedorId = falha ? null : "mock-id",
                Codigo = sucesso ? null : falha ? "ResultadoNaoConfirmado" : "PagamentoRecusado",
                Motivo = sucesso ? null : "Pagamento nao confirmado"
            }
        };
    }

    private static void Processar(OrdensApiFactory factory, MensagemIntegracao mensagem)
    { using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(mensagem); }
}
