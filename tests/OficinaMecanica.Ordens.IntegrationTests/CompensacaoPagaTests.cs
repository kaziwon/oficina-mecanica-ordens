using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using static OficinaMecanica.Ordens.IntegrationTests.LiberacaoExecucaoTests;
using static OficinaMecanica.Ordens.IntegrationTests.InicioReparoTests;
using static OficinaMecanica.Ordens.IntegrationTests.ConclusaoReparoTests;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class CompensacaoPagaTests
{
    [Fact]
    public async Task Cancelar_SoFinalizaDepoisDoEstornoComMesmoPedidoNasRepeticoes()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); Processar(factory, liberacao);
        Processar(factory, Inicio(liberacao)); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var comando = await Solicitar(factory, client);
        Assert.Equal(StatusOrdemServico.EmExecucao, Ordem(factory)); Assert.Equal(3, ContarOutbox(factory));
        await Solicitar(factory, client); Assert.Equal(3, ContarOutbox(factory));
        var cancelado = Responder(comando, "ReparoPagoCancelado"); Processar(factory, cancelado); Processar(factory, cancelado);
        Assert.Equal(StatusOrdemServico.CancelamentoPendente, Ordem(factory));
        var estorno = Outbox(factory, "SolicitarEstorno"); Assert.Equal(cancelado.MessageId, estorno.CausationId);
        Assert.Equal(estorno.MessageId, estorno.Payload.EstornoId); Assert.Equal(4, ContarOutbox(factory));
        var confirmado = Responder(estorno, "EstornoConfirmado"); confirmado.Payload.EstornoProvedorId = "refund-id";
        Processar(factory, confirmado); Processar(factory, confirmado); await Solicitar(factory, client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single(); Assert.Equal(StatusSagaAbertura.CompensadaAposPagamento, saga.Status);
        Assert.Equal(StatusOrdemServico.Cancelada, db.OrdensServico.Single().Status); Assert.Equal("refund-id", saga.EstornoProvedorId);
        Assert.Equal(4, db.Outbox.Count()); Assert.Single(db.HistoricoStatus.Where(item => item.Status == StatusOrdemServico.Cancelada));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/ordensservico/{saga.OrdemServicoId}/entregar", null)).StatusCode);
    }

    [Theory]
    [InlineData("EstornoRecusado", "EstornoRecusado")]
    [InlineData("FalhaProcessamentoEstorno", "ResultadoEstornoNaoConfirmado")]
    public async Task EstornoNaoConfirmado_NaoFingeCancelamentoCompleto(string tipo, string codigo)
    {
        using var factory = await Criar(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var comando = await Solicitar(factory, client); Processar(factory, Responder(comando, "ReparoPagoCancelado"));
        var resultado = Responder(Outbox(factory, "SolicitarEstorno"), tipo); resultado.Payload.Codigo = codigo; resultado.Payload.Motivo = "Verificar no provedor";
        if (tipo == "EstornoRecusado") resultado.Payload.EstornoProvedorId = "refund-recusado";
        Processar(factory, resultado); Processar(factory, resultado);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var saga = db.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.EstornoRequerIntervencao, saga.Status); Assert.Null(saga.EstornadoEm);
        Assert.NotNull(saga.ExecucaoCanceladaEm); Assert.Equal(StatusOrdemServico.CancelamentoPendente, db.OrdensServico.Single().Status);
        Assert.Equal(4, db.Outbox.Count());
    }

    [Fact]
    public async Task CancelamentoRecusado_NaoPedeEstornoEPermiteConclusaoReentregue()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        Processar(factory, liberacao); Processar(factory, inicio); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var comando = await Solicitar(factory, client); var conclusao = Conclusao(inicio);
        Assert.Throws<InvalidOperationException>(() => Processar(factory, conclusao));
        var recusa = Responder(comando, "CancelamentoReparoPagoRecusado"); recusa.Payload.Codigo = "ReparoNaoCancelavel"; recusa.Payload.Motivo = "Ja finalizado";
        Processar(factory, recusa); Processar(factory, conclusao);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var saga = db.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.Finalizada, saga.Status); Assert.NotNull(saga.CompensacaoRecusadaEm); Assert.Null(saga.PedidoEstornoId);
        Assert.Equal(StatusOrdemServico.Finalizada, db.OrdensServico.Single().Status); Assert.Equal(3, db.Outbox.Count());
    }

    [Fact]
    public async Task LiberacaoDuranteCancelamentoRecusado_RetomaOSTambemParaPermitirInicio()
    {
        using var factory = await Criar(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var comando = await Solicitar(factory, client); var liberacao = Resposta(factory, true);
        Processar(factory, liberacao); Assert.Equal(StatusOrdemServico.PagamentoConfirmado, Ordem(factory));
        var recusa = Responder(comando, "CancelamentoReparoPagoRecusado");
        recusa.Payload.Codigo = "ReparoNaoCancelavel"; recusa.Payload.Motivo = "Reserva inconsistente";
        Processar(factory, recusa); Assert.Equal(StatusOrdemServico.AguardandoExecucao, Ordem(factory));
        var inicio = Inicio(liberacao); Processar(factory, inicio); Processar(factory, Conclusao(inicio));
        Assert.Equal(StatusOrdemServico.Finalizada, Ordem(factory)); Assert.Equal(3, ContarOutbox(factory));
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("pagamento")]
    [InlineData("valor")]
    [InlineData("correlacao")]
    public async Task CancelamentoComProvaDivergente_NaoGeraEstorno(string erro)
    {
        using var factory = await Criar(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var resposta = Responder(await Solicitar(factory, client), "ReparoPagoCancelado");
        if (erro == "causa") resposta.CausationId = Guid.NewGuid();
        if (erro == "pagamento") resposta.Payload.PagamentoId = Guid.NewGuid();
        if (erro == "valor") resposta.Payload.ValorTotal = 1m;
        if (erro == "correlacao") resposta.CorrelationId = "outra";
        Assert.Throws<ValidacaoException>(() => Processar(factory, resposta));
        Assert.Equal(3, ContarOutbox(factory)); Assert.Equal(StatusOrdemServico.PagamentoConfirmado, Ordem(factory));
    }

    [Fact]
    public async Task LiberacaoEInicioAtrasados_NaoReabremExecucaoCancelada()
    {
        using var factory = await Criar(); var liberacao = Resposta(factory, true); var inicio = Inicio(liberacao);
        using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client); var comando = await Solicitar(factory, client);
        var cancelado = Responder(comando, "ReparoPagoCancelado"); cancelado.OccurredAt = inicio.OccurredAt.AddSeconds(1);
        Processar(factory, cancelado); Processar(factory, liberacao); Processar(factory, inicio);
        Assert.Equal(StatusOrdemServico.CancelamentoPendente, Ordem(factory)); Assert.Equal(4, ContarOutbox(factory));
    }

    [Fact]
    public async Task Api_ExigeAdminMotivoEOrdemExistente()
    {
        using var factory = await Criar(); using var client = factory.CreateClient();
        var id = Outbox(factory, "LiberarExecucao").Payload.OrdemServicoId;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-reparo", new { motivo = "Teste" })).StatusCode);
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-reparo", new { motivo = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/ordensservico/{Guid.NewGuid()}/cancelar-reparo", new { motivo = "Teste" })).StatusCode);
    }

    [Fact]
    public async Task ReconciliacaoEstorno_ConcluiCancelamentoSemNovoPedidoEIgnoraFalhaAtrasada()
    {
        using var factory = await Criar(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var comando = await Solicitar(factory, client); Processar(factory, Responder(comando, "ReparoPagoCancelado"));
        var estorno = Outbox(factory, "SolicitarEstorno");
        var falha = Responder(estorno, "FalhaProcessamentoEstorno");
        falha.Payload.Codigo = "ResultadoEstornoNaoConfirmado"; falha.Payload.Motivo = "Resultado incerto";
        Processar(factory, falha);
        var confirmado = Responder(estorno, "EstornoConfirmado"); confirmado.Payload.EstornoProvedorId = "refund-original";
        Processar(factory, confirmado); Processar(factory, confirmado);
        falha.MessageId = Guid.NewGuid(); Processar(factory, falha);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = db.SagasAbertura.Single(); Assert.Equal(estorno.MessageId, saga.PedidoEstornoId);
        Assert.Equal(StatusSagaAbertura.CompensadaAposPagamento, saga.Status); Assert.Null(saga.Motivo);
        Assert.Equal(StatusOrdemServico.Cancelada, db.OrdensServico.Single().Status); Assert.Equal(4, db.Outbox.Count());
        Assert.Single(db.HistoricoStatus.Where(item => item.Status == StatusOrdemServico.Cancelada));
    }

    private static async Task<MensagemIntegracao> Solicitar(OrdensApiFactory factory, HttpClient client)
    {
        var id = Outbox(factory, "LiberarExecucao").Payload.OrdemServicoId;
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-reparo", new { motivo = "Desistencia" })).StatusCode);
        return Outbox(factory, "CancelarReparoPago");
    }
    private static MensagemIntegracao Responder(MensagemIntegracao comando, string tipo)
    {
        var resposta = MensagemSerializer.Desserializar(MensagemSerializer.Serializar(comando));
        resposta.MessageId = Guid.NewGuid(); resposta.CausationId = comando.MessageId; resposta.Type = tipo; resposta.OccurredAt = DateTime.UtcNow;
        resposta.Payload.Motivo = null; return resposta;
    }
    private static MensagemIntegracao Outbox(OrdensApiFactory factory, string tipo)
    {
        using var scope = factory.Services.CreateScope(); return scope.ServiceProvider.GetRequiredService<AppDbContext>().Outbox.AsNoTracking()
            .AsEnumerable().Select(item => MensagemSerializer.Desserializar(item.Body)).Single(item => item.Type == tipo);
    }
    private static int ContarOutbox(OrdensApiFactory factory)
    { using var scope = factory.Services.CreateScope(); return scope.ServiceProvider.GetRequiredService<AppDbContext>().Outbox.Count(); }
    private static StatusOrdemServico Ordem(OrdensApiFactory factory)
    { using var scope = factory.Services.CreateScope(); return scope.ServiceProvider.GetRequiredService<AppDbContext>().OrdensServico.Single().Status; }
}
