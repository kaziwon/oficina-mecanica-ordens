using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using static OficinaMecanica.Ordens.IntegrationTests.LiberacaoExecucaoTests;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class InicioReparoTests
{
    [Fact]
    public async Task InicioConfirmado_AtualizaSagaOSHistoricoSemRepetir()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); Processar(factory, liberacao);
        var inicio = Inicio(liberacao); Processar(factory, inicio); Processar(factory, inicio);
        // Uma reentrega com novo ID, mas mesmo fato, tambem nao duplica historico.
        inicio.MessageId = Guid.NewGuid(); Processar(factory, inicio);
        liberacao.MessageId = Guid.NewGuid(); Processar(factory, liberacao);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.EmExecucao, saga.Status); Assert.Equal(inicio.OccurredAt, saga.ReparoIniciadoEm);
        Assert.Equal(StatusOrdemServico.EmExecucao, db.OrdensServico.Single().Status); Assert.Equal(7, db.HistoricoStatus.Count());
        Assert.Equal(2, db.Outbox.Count()); Assert.NotNull(saga.PagamentoConfirmadoEm);
    }

    [Fact]
    public async Task InicioAntesDaLiberacao_PermiteReentregaDepoisSemAvancarPrematuramente()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Assert.Throws<InvalidOperationException>(() => Processar(factory, inicio));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Single(db.Inbox);
            Assert.Equal(StatusSagaAbertura.LiberacaoExecucaoPendente, db.SagasAbertura.Single().Status);
        }
        Processar(factory, liberacao); Processar(factory, inicio);
        using var verificar = factory.Services.CreateScope();
        Assert.Equal(StatusSagaAbertura.EmExecucao, verificar.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.Single().Status);
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("pagamento")]
    [InlineData("valor")]
    [InlineData("correlacao")]
    [InlineData("data")]
    public async Task InicioDivergente_NaoAvancaNemGravaInbox(string erro)
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); Processar(factory, liberacao); var inicio = Inicio(liberacao);
        if (erro == "causa") inicio.CausationId = Guid.NewGuid();
        if (erro == "pagamento") inicio.Payload.PagamentoId = Guid.NewGuid();
        if (erro == "valor") inicio.Payload.ValorTotal = 1m;
        if (erro == "correlacao") inicio.CorrelationId = "outra";
        if (erro == "data") inicio.Payload.ReparoIniciadoEm = inicio.OccurredAt.AddSeconds(1);
        Assert.Throws<ValidacaoException>(() => Processar(factory, inicio));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, db.Inbox.Count()); Assert.Equal(StatusOrdemServico.AguardandoExecucao, db.OrdensServico.Single().Status);
    }

    internal static MensagemIntegracao Inicio(MensagemIntegracao liberacao)
    {
        var agora = DateTime.UtcNow.AddMilliseconds(1);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "ReparoIniciado",
            SagaId = liberacao.SagaId,
            CausationId = liberacao.CausationId,
            CorrelationId = liberacao.CorrelationId,
            OccurredAt = agora,
            Payload = new DadosMensagem
            {
                OrdemServicoId = liberacao.Payload.OrdemServicoId,
                ExecucaoId = liberacao.Payload.ExecucaoId,
                OrcamentoId = liberacao.Payload.OrcamentoId,
                PagamentoId = liberacao.Payload.PagamentoId,
                ProvedorId = liberacao.Payload.ProvedorId,
                ValorTotal = liberacao.Payload.ValorTotal,
                Moeda = "BRL",
                ReparoIniciadoEm = agora
            }
        };
    }
}
