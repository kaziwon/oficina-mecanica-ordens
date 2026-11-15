using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.Messaging;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class WorkerObservabilityTests
{
    [SqsTheory]
    [InlineData("RegistrarExecucao", false, true)]
    [InlineData("RegistrarExecucao", false, false)]
    [InlineData("GerarOrcamento", true, true)]
    [InlineData("GerarOrcamento", true, false)]
    [InlineData("SolicitarPagamento", true, true)]
    [InlineData("SolicitarPagamento", true, false)]
    [InlineData("SolicitarEstorno", true, true)]
    [InlineData("SolicitarEstorno", true, false)]
    public async Task PublicacaoContinuaConfirmadaSomenteDepoisDoEnvio(string tipo, bool paraOrcamentos, bool informacoesHabilitadas)
    {
        using var client = Cliente();
        var entrada = "worker-in-" + Guid.NewGuid().ToString("N");
        var saida = "worker-out-" + Guid.NewGuid().ToString("N");
        var entradaUrl = (await client.CreateQueueAsync(entrada)).QueueUrl;
        var saidaUrl = (await client.CreateQueueAsync(saida)).QueueUrl;
        var orcamentos = "worker-orc-" + Guid.NewGuid().ToString("N");
        var orcamentosUrl = (await client.CreateQueueAsync(orcamentos)).QueueUrl;
        var configuracao = Configuracao(entrada, saida);
        configuracao.OrcamentosQueue = orcamentos;
        var mensagem = Mensagem(tipo);
        var outbox = new OutboxFake(mensagem);
        using var services = new ServiceCollection().AddSingleton<IOutboxGateway>(outbox).BuildServiceProvider();
        using var transport = new SqsTransport(Options.Create(configuracao));
        var logger = new LoggerTeste(informacoesHabilitadas);
        using var worker = new IntegracaoWorker(services.GetRequiredService<IServiceScopeFactory>(), transport,
            Options.Create(configuracao), logger);
        try
        {
            await worker.StartAsync(CancellationToken.None);
            Assert.Equal(mensagem.MessageId, await outbox.Confirmada.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            await worker.StopAsync(CancellationToken.None);
            var response = await client.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = paraOrcamentos ? orcamentosUrl : saidaUrl, MaxNumberOfMessages = 1, WaitTimeSeconds = 1
            });
            var recebida = Assert.Single(response.Messages);
            Assert.Equal(mensagem.MessageId, MensagemSerializer.Desserializar(recebida.Body).MessageId);
            Assert.False(logger.Falha.Task.IsCompleted);
            Assert.Equal(informacoesHabilitadas, logger.QuantidadeInformacoes > 0);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await client.DeleteQueueAsync(entradaUrl);
            await client.DeleteQueueAsync(saidaUrl);
            await client.DeleteQueueAsync(orcamentosUrl);
        }
    }

    [SqsFact]
    public async Task FalhaNoProcessamentoPreservaMensagemParaReentrega()
    {
        using var client = Cliente();
        var entrada = "worker-in-" + Guid.NewGuid().ToString("N");
        var saida = "worker-out-" + Guid.NewGuid().ToString("N");
        var entradaUrl = (await client.CreateQueueAsync(entrada)).QueueUrl;
        var saidaUrl = (await client.CreateQueueAsync(saida)).QueueUrl;
        using var services = new ServiceCollection().AddSingleton<IOutboxGateway>(new OutboxFake(null)).BuildServiceProvider();
        using var transport = new SqsTransport(Options.Create(Configuracao(entrada, saida)));
        var logger = new LoggerTeste();
        using var worker = new IntegracaoWorker(services.GetRequiredService<IServiceScopeFactory>(), transport,
            Options.Create(Configuracao(entrada, saida)), logger);
        try
        {
            await client.SendMessageAsync(entradaUrl, MensagemSerializer.Serializar(Mensagem("RespostaTeste")));
            await worker.StartAsync(CancellationToken.None);
            await logger.Falha.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await worker.StopAsync(CancellationToken.None);
            var attributes = await client.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = entradaUrl, AttributeNames = ["ApproximateNumberOfMessagesNotVisible"]
            });
            Assert.Equal("1", attributes.Attributes["ApproximateNumberOfMessagesNotVisible"]);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await client.DeleteQueueAsync(entradaUrl);
            await client.DeleteQueueAsync(saidaUrl);
        }
    }

    [SqsTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessamentoBemSucedidoPersisteAntesDeConfirmar(bool informacoesHabilitadas)
    {
        using var factory = new OrdensApiFactory();
        using var http = factory.CreateClient();
        await ApiTestHelper.Autenticar(http);
        (await http.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();
        MensagemIntegracao mensagem;
        using (var scope = factory.Services.CreateScope())
        {
            var saga = scope.ServiceProvider.GetRequiredService<AppDbContext>().SagasAbertura.Single();
            mensagem = new MensagemIntegracao
            {
                MessageId = Guid.NewGuid(), Type = "ExecucaoRegistrada", SagaId = saga.Id,
                CausationId = saga.PedidoRegistroId, CorrelationId = saga.CorrelationId, OccurredAt = DateTime.UtcNow,
                Payload = new DadosMensagem { OrdemServicoId = saga.OrdemServicoId, ExecucaoId = Guid.NewGuid() }
            };
        }
        using var client = Cliente();
        var entrada = "worker-ok-in-" + Guid.NewGuid().ToString("N");
        var saida = "worker-ok-out-" + Guid.NewGuid().ToString("N");
        var entradaUrl = (await client.CreateQueueAsync(entrada)).QueueUrl;
        var saidaUrl = (await client.CreateQueueAsync(saida)).QueueUrl;
        var configuracao = Configuracao(entrada, saida);
        using var transport = new SqsTransport(Options.Create(configuracao));
        var logger = new LoggerTeste(informacoesHabilitadas);
        using var worker = new IntegracaoWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), transport,
            Options.Create(configuracao), logger);
        try
        {
            await client.SendMessageAsync(entradaUrl, MensagemSerializer.Serializar(mensagem));
            await worker.StartAsync(CancellationToken.None);
            await AguardarConfirmacao(client, entradaUrl);
            await worker.StopAsync(CancellationToken.None);
            using var scope = factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saga = Assert.Single(database.SagasAbertura);
            Assert.Equal(StatusSagaAbertura.RegistroConfirmado, saga.Status);
            Assert.Equal(mensagem.Payload.ExecucaoId, saga.ExecucaoId);
            Assert.Equal(mensagem.MessageId, Assert.Single(database.Inbox).Id);
            Assert.False(logger.Falha.Task.IsCompleted);
            Assert.Equal(informacoesHabilitadas, logger.QuantidadeInformacoes > 0);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await client.DeleteQueueAsync(entradaUrl);
            await client.DeleteQueueAsync(saidaUrl);
        }
    }

    private static async Task AguardarConfirmacao(AmazonSQSClient client, string filaUrl)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var response = await client.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = filaUrl, AttributeNames = ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
            }, timeout.Token);
            if (response.Attributes.Values.All(value => value == "0")) return;
            await Task.Delay(25, timeout.Token);
        }
    }

    private static AmazonSQSClient Cliente() => new(new BasicAWSCredentials("local", "local"),
        new AmazonSQSConfig { ServiceURL = Environment.GetEnvironmentVariable("SQS_TEST_ENDPOINT")!, AuthenticationRegion = "us-east-1" });

    private static MessagingSettings Configuracao(string entrada, string saida) => new()
    {
        Enabled = true, Endpoint = Environment.GetEnvironmentVariable("SQS_TEST_ENDPOINT")!,
        ComandosQueue = saida, OrcamentosQueue = saida, EventosQueue = entrada
    };

    private static MensagemIntegracao Mensagem(string type) => new()
    {
        MessageId = Guid.NewGuid(), SagaId = Guid.NewGuid(), Type = type,
        OccurredAt = DateTime.UtcNow, CorrelationId = "worker-observabilidade"
    };

    private sealed class OutboxFake(MensagemIntegracao? mensagem) : IOutboxGateway
    {
        private MensagemIntegracao? _mensagem = mensagem;
        public TaskCompletionSource<Guid> Confirmada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<MensagemIntegracao> ObterPendentes(int limite)
        {
            var pendente = Volatile.Read(ref _mensagem);
            return pendente is null ? [] : [pendente];
        }
        public void ConfirmarPublicacao(Guid messageId)
        {
            Interlocked.Exchange(ref _mensagem, null);
            Confirmada.TrySetResult(messageId);
        }
    }

    private sealed class LoggerTeste(bool informacoesHabilitadas = true) : ILogger<IntegracaoWorker>
    {
        private int _quantidadeInformacoes;
        public int QuantidadeInformacoes => Volatile.Read(ref _quantidadeInformacoes);
        public TaskCompletionSource<bool> Falha { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.Information || informacoesHabilitadas;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information) Interlocked.Increment(ref _quantidadeInformacoes);
            if (logLevel == LogLevel.Error) Falha.TrySetResult(true);
        }
    }
}
