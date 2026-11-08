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

public class DiagnosticoOrcamentoTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Conclusao_ComOuSemInicioAnterior_SolicitaUmOrcamentoESoAvancaAposConfirmacao(bool comInicio)
    {
        using var factory = await Criar();
        var conclusao = Evento(factory);
        var inicio = Inicio(conclusao);
        if (comInicio) Processar(factory, inicio);
        Processar(factory, conclusao);
        Processar(factory, conclusao);
        Processar(factory, inicio);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(StatusSagaAbertura.OrcamentoPendente, db.SagasAbertura.Single().Status);
            Assert.Equal(StatusOrdemServico.EmDiagnostico, db.OrdensServico.Single().Status);
            Assert.Equal(2, db.Outbox.Count());
            Assert.Equal(2, db.HistoricoStatus.Count());
            var comando = MensagemSerializer.Desserializar(db.Outbox.Single(item => item.Id != db.SagasAbertura.Single().PedidoRegistroId).Body);
            Assert.Equal("GerarOrcamento", comando.Type);
            Assert.Equal(conclusao.MessageId, comando.CausationId);
            Assert.Equal(125m, Assert.Single(comando.Payload.Itens!).PrecoUnitario);
        }
        var resposta = Resposta(factory);
        Processar(factory, resposta);
        Processar(factory, resposta);
        Processar(factory, Inicio(conclusao));
        // A confirmacao de registro tambem pode chegar depois do diagnostico e nao deve regredir a Saga.
        var registro = Inicio(conclusao);
        registro.Type = "ExecucaoRegistrada";
        registro.Payload.DiagnosticoIniciadoEm = null;
        Processar(factory, registro);
        using var verificar = factory.Services.CreateScope();
        var banco = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        var saga = banco.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.OrcamentoGerado, saga.Status);
        Assert.Equal(resposta.Payload.OrcamentoId, saga.OrcamentoId);
        Assert.Equal(125m, saga.ValorOrcamento);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, banco.OrdensServico.Single().Status);
        Assert.Equal(3, banco.HistoricoStatus.Count());
        Assert.Equal(2, banco.Outbox.Count());
    }

    [Fact]
    public async Task Recusa_PreservaDiagnosticoENaoAvancaParaAprovacao()
    {
        using var factory = await Criar();
        Processar(factory, Evento(factory));
        var resposta = Resposta(factory);
        resposta.Type = "GeracaoOrcamentoRecusada";
        resposta.Payload.OrcamentoId = null;
        resposta.Payload.ValorTotal = null;
        resposta.Payload.Codigo = "DadosInvalidos";
        resposta.Payload.Motivo = "Informe pelo menos um item.";
        Processar(factory, resposta);
        Processar(factory, resposta);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(StatusSagaAbertura.FalhaRequerIntervencao, db.SagasAbertura.Single().Status);
        Assert.Equal(StatusOrdemServico.EmDiagnostico, db.OrdensServico.Single().Status);
        Assert.Null(db.SagasAbertura.Single().OrcamentoId);
    }

    [Theory]
    [InlineData("causa")]
    [InlineData("execucao")]
    [InlineData("correlacao")]
    [InlineData("datas")]
    public async Task EventoIncompativel_NaoGravaInboxNemAlteraStatus(string alteracao)
    {
        using var factory = await Criar();
        var evento = Evento(factory);
        if (alteracao == "causa") evento.CausationId = Guid.NewGuid();
        if (alteracao == "execucao") evento.Payload.ExecucaoId = null;
        if (alteracao == "correlacao") evento.CorrelationId = "outra";
        if (alteracao == "datas") evento.Payload.DiagnosticoConcluidoEm = evento.Payload.DiagnosticoIniciadoEm!.Value.AddSeconds(-1);
        Assert.Throws<ValidacaoException>(() => Processar(factory, evento));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.Inbox);
        Assert.Single(db.Outbox);
        Assert.Equal(StatusOrdemServico.Recebida, db.OrdensServico.Single().Status);
    }

    [Fact]
    public async Task Orcamento_ComCausaErradaNaoAprovaOS()
    {
        using var factory = await Criar();
        Processar(factory, Evento(factory));
        var resposta = Resposta(factory);
        resposta.CausationId = Guid.NewGuid();
        Assert.Throws<ValidacaoException>(() => Processar(factory, resposta));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(StatusOrdemServico.EmDiagnostico,
            scope.ServiceProvider.GetRequiredService<AppDbContext>().OrdensServico.Single().Status);
    }

    [Fact]
    public async Task RecusaDaCompensacaoAntesDoDiagnostico_MantemIntervencaoEMostraTrabalhoReal()
    {
        using var factory = await Criar();
        var conclusao = Evento(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var source = new EfSagaAberturaDataSource(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            var saga = source.ObterPorId(conclusao.SagaId)!;
            saga.SolicitarCancelamento("Desistencia"); saga.ConcluirCancelamento(false, "Diagnostico ja iniciado");
            source.Salvar(saga, null, null, false);
        }
        Processar(factory, conclusao);
        Processar(factory, Inicio(conclusao));
        using var verificar = factory.Services.CreateScope();
        var banco = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistida = banco.SagasAbertura.Single();
        Assert.Equal(StatusSagaAbertura.FalhaRequerIntervencao, persistida.Status);
        Assert.Null(persistida.PedidoOrcamentoId);
        Assert.NotNull(persistida.DiagnosticoConcluidoEm);
        Assert.Equal(StatusOrdemServico.EmDiagnostico, banco.OrdensServico.Single().Status);
        Assert.Single(banco.Outbox);
    }

    [Fact]
    public async Task Concorrencia_NaoGravaSegundoPedidoNemInboxParcial()
    {
        using var factory = await Criar();
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var sourceA = new EfSagaAberturaDataSource(dbA); var sourceB = new EfSagaAberturaDataSource(dbB);
        var sagaA = dbA.SagasAbertura.Single(); var sagaB = sourceB.ObterPorId(sagaA.Id)!;
        var evento = Guid.NewGuid(); var execucao = Guid.NewGuid(); var inicio = DateTime.UtcNow;
        sagaA.SolicitarOrcamento(evento, execucao, inicio, inicio);
        sagaB.SolicitarOrcamento(evento, execucao, inicio, inicio);
        var mensagem = new MensagemIntegracao
        {
            MessageId = sagaA.PedidoOrcamentoId!.Value,
            Type = "GerarOrcamento",
            SagaId = sagaA.Id,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = sagaA.CorrelationId,
            Payload = new DadosMensagem { OrdemServicoId = sagaA.OrdemServicoId }
        };
        sourceA.Salvar(sagaA, evento, mensagem, false, StatusOrdemServico.EmDiagnostico);
        mensagem.MessageId = sagaB.PedidoOrcamentoId!.Value;
        Assert.Throws<DbUpdateConcurrencyException>(() => sourceB.Salvar(sagaB, Guid.NewGuid(), mensagem,
            false, StatusOrdemServico.EmDiagnostico));
        using var verificar = factory.Services.CreateScope();
        var banco = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(banco.Inbox); Assert.Equal(2, banco.Outbox.Count());
        Assert.Equal(2, banco.HistoricoStatus.Count());
        Assert.Equal(sagaA.PedidoOrcamentoId, banco.SagasAbertura.Single().PedidoOrcamentoId);
    }

    private static async Task<OrdensApiFactory> Criar()
    {
        var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        return factory;
    }

    private static MensagemIntegracao Evento(OrdensApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.AsNoTracking().Single();
        var inicio = DateTime.UtcNow.AddMinutes(-1);
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "DiagnosticoConcluido",
            SagaId = saga.Id,
            CausationId = saga.PedidoRegistroId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = Guid.NewGuid(),
                Diagnostico = "Trocar pastilhas",
                DiagnosticoIniciadoEm = inicio,
                DiagnosticoConcluidoEm = DateTime.UtcNow,
                Itens = [new ItemMensagem { Tipo = "Servico", ReferenciaId = Guid.NewGuid(), Nome = "Revisao", Quantidade = 1, PrecoUnitario = 125m }]
            }
        };
    }

    private static MensagemIntegracao Inicio(MensagemIntegracao conclusao) => new()
    {
        MessageId = Guid.NewGuid(),
        Type = "DiagnosticoIniciado",
        SagaId = conclusao.SagaId,
        CausationId = conclusao.CausationId,
        CorrelationId = conclusao.CorrelationId,
        OccurredAt = DateTime.UtcNow,
        Payload = new DadosMensagem
        {
            OrdemServicoId = conclusao.Payload.OrdemServicoId,
            ExecucaoId = conclusao.Payload.ExecucaoId,
            DiagnosticoIniciadoEm = conclusao.Payload.DiagnosticoIniciadoEm
        }
    };

    private static MensagemIntegracao Resposta(OrdensApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.AsNoTracking().Single();
        return new MensagemIntegracao
        {
            MessageId = Guid.NewGuid(),
            Type = "OrcamentoGerado",
            SagaId = saga.Id,
            CausationId = saga.PedidoOrcamentoId,
            CorrelationId = saga.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Payload = new DadosMensagem
            {
                OrdemServicoId = saga.OrdemServicoId,
                ExecucaoId = saga.ExecucaoId,
                OrcamentoId = Guid.NewGuid(),
                ValorTotal = 125m
            }
        };
    }

    private static void Processar(OrdensApiFactory factory, MensagemIntegracao mensagem)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(mensagem);
    }
}
