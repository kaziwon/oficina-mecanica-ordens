using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class SagaAberturaApiTests
{
    [Fact]
    public async Task Criacao_SalvaSagaEOutboxMesmoSemFilaDisponivel()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "saga-teste-123");
        var ordem = await Criar(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = Assert.Single(database.SagasAbertura);
        var outbox = new EfOutboxDataSource(database);
        var mensagem = Assert.Single(outbox.ObterPendentes(10));
        Assert.Equal(ordem.Id, saga.OrdemServicoId);
        Assert.Equal("saga-teste-123", mensagem.CorrelationId);
        Assert.Equal(saga.Id, mensagem.SagaId);
        Assert.Equal("RegistrarExecucao", mensagem.Type);
        var response = await client.GetFromJsonAsync<SagaAberturaResponseDto>($"/api/ordensservico/{ordem.Id}/saga-abertura", ApiTestHelper.Json);
        Assert.Equal(StatusSagaAbertura.AguardandoRegistro, response!.Status);
        outbox.ConfirmarPublicacao(mensagem.MessageId);
        Assert.Empty(outbox.ObterPendentes(10));
        Assert.Single(database.Outbox);
    }

    [Fact]
    public async Task Confirmacao_RepetidaNaoDuplicaInboxNemIniciaDiagnostico()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var ordem = await Criar(client);
        var resposta = Resposta(factory, "ExecucaoRegistrada");
        Processar(factory, resposta);
        Processar(factory, resposta);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.RegistroConfirmado, database.SagasAbertura.Single().Status);
        Assert.Equal(StatusOrdemServico.Recebida, database.OrdensServico.Single().Status);
        Assert.Single(database.Inbox);
        Assert.Single(database.HistoricoStatus);
        resposta.MessageId = Guid.NewGuid();
        resposta.CausationId = Guid.NewGuid();
        Assert.Throws<ValidacaoException>(() => Processar(factory, resposta));
    }

    [Fact]
    public async Task Compensacao_AguardaConfirmacaoEPreservaCadastrosEHistorico()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var ordem = await Criar(client);
        var respostaRegistro = Resposta(factory, "ExecucaoRegistrada");
        (await client.PostAsJsonAsync($"/api/ordensservico/{ordem.Id}/cancelar-entrada", new { motivo = "Desistencia" })).EnsureSuccessStatusCode();
        Processar(factory, respostaRegistro);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(StatusSagaAbertura.CompensacaoPendente, database.SagasAbertura.Single().Status);
            Assert.Equal(StatusOrdemServico.Recebida, database.OrdensServico.Single().Status);
            Assert.Equal(2, database.Outbox.Count());
        }
        var cancelada = Resposta(factory, "EntradaExecucaoCancelada");
        Processar(factory, cancelada);
        Processar(factory, cancelada);
        respostaRegistro.MessageId = Guid.NewGuid();
        Processar(factory, respostaRegistro);
        using var verification = factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.Compensada, persisted.SagasAbertura.Single().Status);
        Assert.Equal(StatusOrdemServico.Cancelada, persisted.OrdensServico.Single().Status);
        Assert.Equal(2, persisted.HistoricoStatus.Count());
        Assert.Single(persisted.Clientes);
        Assert.Single(persisted.Veiculos);
        (await client.PostAsJsonAsync($"/api/ordensservico/{ordem.Id}/cancelar-entrada", new { motivo = "Repeticao" })).EnsureSuccessStatusCode();
        Assert.Equal(2, persisted.Outbox.Count());
    }

    [Theory]
    [InlineData("RegistroExecucaoRecusado", "ProcessoCancelado", StatusSagaAbertura.RegistroRecusado, StatusOrdemServico.Cancelada)]
    [InlineData("RegistroExecucaoRecusado", "ConflitoDeVinculo", StatusSagaAbertura.FalhaRequerIntervencao, StatusOrdemServico.Recebida)]
    [InlineData("CancelamentoEntradaExecucaoRecusado", "DiagnosticoJaIniciado", StatusSagaAbertura.FalhaRequerIntervencao, StatusOrdemServico.Recebida)]
    public async Task Recusa_DistingueRegistroDeCompensacao(string tipo, string codigo, StatusSagaAbertura statusSaga, StatusOrdemServico statusOrdem)
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var ordem = await Criar(client);
        if (tipo.StartsWith("Cancelamento", StringComparison.Ordinal))
            (await client.PostAsJsonAsync($"/api/ordensservico/{ordem.Id}/cancelar-entrada", new { motivo = "Desistencia" })).EnsureSuccessStatusCode();
        var resposta = Resposta(factory, tipo);
        resposta.Payload.Codigo = codigo;
        resposta.Payload.Motivo = "Requer tratamento";
        Processar(factory, resposta);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(statusSaga, database.SagasAbertura.Single().Status);
        Assert.Equal(statusOrdem, database.OrdensServico.Single().Status);
    }

    [Fact]
    public async Task RotasSaga_ExigemAdminEValidamMotivoERecurso()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/ordensservico/{id}/saga-abertura")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-entrada", new { motivo = "Teste" })).StatusCode);
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/ordensservico/{id}/saga-abertura")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-entrada", new { motivo = "Teste" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/ordensservico/{id}/cancelar-entrada", new { motivo = " " })).StatusCode);
    }

    private static async Task<OrdemServicoResponseDto> Criar(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
    }

    [Fact]
    public async Task RespostaConcorrente_NaoSobrescreveCompensacaoENaoConfirmaInboxParcial()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var ordem = await Criar(client);
        using var primeiroScope = factory.Services.CreateScope();
        using var segundoScope = factory.Services.CreateScope();
        var primeiro = primeiroScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var segundo = segundoScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sourceA = new EfSagaAberturaDataSource(primeiro);
        var sourceB = new EfSagaAberturaDataSource(segundo);
        var sagaA = sourceA.ObterPorOrdemServicoId(ordem.Id)!;
        var sagaB = sourceB.ObterPorOrdemServicoId(ordem.Id)!;
        sagaA.SolicitarCancelamento("Desistencia");
        sourceA.Salvar(sagaA, null, MensagensAbertura.Cancelar(sagaA), false);
        sagaB.ConfirmarRegistro(Guid.NewGuid());
        Assert.Throws<DbUpdateConcurrencyException>(() => sourceB.Salvar(sagaB, Guid.NewGuid(), null, false));
        using var verification = factory.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.CompensacaoPendente, database.SagasAbertura.Single().Status);
        Assert.Empty(database.Inbox);
        Assert.Equal(2, database.Outbox.Count());
    }

    private static MensagemIntegracao Resposta(OrdensApiFactory factory, string tipo)
    {
        using var scope = factory.Services.CreateScope();
        var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.Single();
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = tipo,
            SagaId = saga.Id,
            CausationId = tipo is "ExecucaoRegistrada" or "RegistroExecucaoRecusado" ? saga.PedidoRegistroId : saga.PedidoCancelamentoId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = tipo == "ExecucaoRegistrada" ? saga.ExecucaoId ?? Guid.NewGuid()
                    : tipo == "EntradaExecucaoCancelada" ? saga.ExecucaoId : null
            }
        };
    }

    private static void Processar(OrdensApiFactory factory, MensagemIntegracao resposta)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(resposta);
    }
}
