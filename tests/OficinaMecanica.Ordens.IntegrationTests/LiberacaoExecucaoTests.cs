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

public class LiberacaoExecucaoTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resposta_AtualizaOSSomenteSeExecucaoAceitou(bool aceita)
    {
        using var factory = await Criar(); var resposta = Resposta(factory, aceita);
        Processar(factory, resposta); Processar(factory, resposta);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single();
        Assert.Equal(aceita ? StatusSagaAbertura.ExecucaoLiberada : StatusSagaAbertura.FalhaRequerIntervencao, saga.Status);
        Assert.Equal(aceita ? StatusOrdemServico.AguardandoExecucao : StatusOrdemServico.PagamentoConfirmado, db.OrdensServico.Single().Status);
        Assert.Equal(aceita ? 6 : 5, db.HistoricoStatus.Count()); Assert.Equal(2, db.Inbox.Count()); Assert.Equal(2, db.Outbox.Count());
        Assert.NotNull(saga.PagamentoConfirmadoEm); Assert.Equal(resposta.Payload.PagamentoId, saga.PagamentoId);
        if (aceita) Assert.NotNull(saga.ExecucaoLiberadaEm); else Assert.Null(saga.ExecucaoLiberadaEm);
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("pagamento")]
    [InlineData("valor")]
    [InlineData("correlacao")]
    public async Task RespostaDivergente_NaoLiberaOS(string erro)
    {
        using var factory = await Criar(); var resposta = Resposta(factory, true);
        if (erro == "causa") resposta.CausationId = Guid.NewGuid();
        if (erro == "pagamento") resposta.Payload.PagamentoId = Guid.NewGuid();
        if (erro == "valor") resposta.Payload.ValorTotal = 199m;
        if (erro == "correlacao") resposta.CorrelationId = "outra";
        Assert.Throws<ValidacaoException>(() => Processar(factory, resposta));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(db.Inbox); Assert.Equal(StatusOrdemServico.PagamentoConfirmado, db.OrdensServico.Single().Status);
    }

    [Fact]
    public async Task PagamentoAtrasado_NaoCriaOutraLiberacaoNemRegrideFila()
    {
        using var factory = await Criar(); var resposta = Resposta(factory, true); Processar(factory, resposta);
        MensagemIntegracao pagamento;
        using (var scope = factory.Services.CreateScope())
        {
            var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.Single();
            pagamento = new MensagemIntegracao
            {
                MessageId = Guid.NewGuid(),
                Type = "PagamentoConfirmado",
                SagaId = saga.Id,
                CausationId = saga.PedidoPagamentoId,
                CorrelationId = saga.CorrelationId,
                OccurredAt = DateTime.UtcNow,
                Payload = new DadosMensagem
                {
                    OrdemServicoId = saga.OrdemServicoId,
                    ExecucaoId = saga.ExecucaoId,
                    OrcamentoId = saga.OrcamentoId,
                    PagamentoId = saga.PagamentoId,
                    ProvedorId = saga.PagamentoProvedorId,
                    ValorTotal = 200m,
                    Moeda = "BRL"
                }
            };
        }
        Processar(factory, pagamento);
        using var verificar = factory.Services.CreateScope(); var db = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.ExecucaoLiberada, db.SagasAbertura.Single().Status);
        Assert.Equal(StatusOrdemServico.AguardandoExecucao, db.OrdensServico.Single().Status);
        Assert.Equal(2, db.Outbox.Count()); Assert.Equal(6, db.HistoricoStatus.Count());
    }

    [Fact]
    public async Task RespostasConcorrentes_NaoSobrescrevemLiberacaoConfirmada()
    {
        using var factory = await Criar(); using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sagaA = dbA.SagasAbertura.Single(); var sagaB = dbB.SagasAbertura.Single(); var agora = DateTime.UtcNow;
        sagaA.RegistrarRespostaLiberacao(true, agora, null); sagaB.RegistrarRespostaLiberacao(false, agora, "Recusa concorrente");
        new EfSagaAberturaDataSource(dbA).Salvar(sagaA, Guid.NewGuid(), null, false, StatusOrdemServico.AguardandoExecucao, agora);
        Assert.Throws<DbUpdateConcurrencyException>(() => new EfSagaAberturaDataSource(dbB).Salvar(sagaB, Guid.NewGuid(), null, false));
        using var verificar = factory.Services.CreateScope(); var db = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.ExecucaoLiberada, db.SagasAbertura.Single().Status);
        Assert.Equal(2, db.Inbox.Count()); Assert.Equal(6, db.HistoricoStatus.Count());
    }

    [Fact]
    public async Task RecusaDepoisDaConfirmacao_NaoCancelaPagamentoNemFila()
    {
        using var factory = await Criar(); var resposta = Resposta(factory, true); Processar(factory, resposta);
        resposta.MessageId = Guid.NewGuid(); resposta.Type = "LiberacaoExecucaoRecusada";
        resposta.Payload.Codigo = "ExecucaoNaoLiberavel"; resposta.Payload.Motivo = "Resposta contraditoria";
        Assert.Throws<InvalidOperationException>(() => Processar(factory, resposta));
    }

    internal static async Task<OrdensApiFactory> Criar()
    {
        var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single(); var inicio = DateTime.UtcNow.AddMinutes(-1); var agora = DateTime.UtcNow;
        saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), inicio, inicio.AddSeconds(1)); saga.ConfirmarOrcamento(Guid.NewGuid(), 200m);
        saga.ResponderOrcamento(Guid.NewGuid(), true, inicio.AddSeconds(2), inicio.AddSeconds(3), null);
        saga.RegistrarResultadoPagamento(Guid.NewGuid(), "mock-id", true, agora, null);
        var evento = Guid.NewGuid(); saga.SolicitarLiberacaoExecucao(evento);
        db.OrdensServico.Include(item => item.Historico).Single().AguardarPagamento(inicio, inicio.AddSeconds(2), inicio.AddSeconds(3));
        var comando = new MensagemIntegracao
        {
            MessageId = saga.PedidoLiberacaoExecucaoId!.Value,
            Type = "LiberarExecucao",
            SagaId = saga.Id,
            CausationId = evento,
            CorrelationId = saga.CorrelationId,
            OccurredAt = agora,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = saga.OrcamentoId,
                PagamentoId = saga.PagamentoId,
                ProvedorId = saga.PagamentoProvedorId,
                ValorTotal = 200m,
                Moeda = "BRL"
            }
        };
        new EfSagaAberturaDataSource(db).Salvar(saga, evento, comando, false, StatusOrdemServico.PagamentoConfirmado, agora);
        return factory;
    }

    internal static MensagemIntegracao Resposta(OrdensApiFactory factory, bool aceita)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var saga = db.SagasAbertura.AsNoTracking().Single();
        var comando = MensagemSerializer.Desserializar(db.Outbox.Single(item => item.Id == saga.PedidoLiberacaoExecucaoId).Body);
        Assert.Equal(saga.EventoPagamentoConfirmadoId, comando.CausationId);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = aceita ? "ExecucaoLiberada" : "LiberacaoExecucaoRecusada",
            SagaId = saga.Id,
            CausationId = comando.MessageId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = saga.OrcamentoId,
                PagamentoId = saga.PagamentoId,
                ProvedorId = saga.PagamentoProvedorId,
                ValorTotal = 200m,
                Moeda = "BRL",
                Codigo = aceita ? null : "ExecucaoNaoLiberavel",
                Motivo = aceita ? null : "Execucao nao liberavel"
            }
        };
    }

    internal static void Processar(OrdensApiFactory factory, MensagemIntegracao mensagem)
    { using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(mensagem); }
}
