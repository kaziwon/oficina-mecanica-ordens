using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

namespace OficinaMecanica.Ordens.Messaging;

public class IntegracaoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SqsTransport _transport;
    private readonly MessagingSettings _settings;
    private readonly ILogger<IntegracaoWorker> _logger;

    public IntegracaoWorker(IServiceScopeFactory scopes, SqsTransport transport,
        IOptions<MessagingSettings> settings, ILogger<IntegracaoWorker> logger)
    {
        _scopes = scopes; _transport = transport; _settings = settings.Value; _logger = logger;
    }

    private async Task PublicarMensagem(MensagemIntegracao mensagem, IOutboxGateway outbox, CancellationToken token)
    {
        using var logScope = CriarEscopo(mensagem);
        var destino = mensagem.Type is "GerarOrcamento" or "SolicitarPagamento" or "SolicitarEstorno" ? _settings.OrcamentosQueue : _settings.ComandosQueue;
        await _transport.Enviar(destino, MensagemSerializer.Serializar(mensagem), token);
        outbox.ConfirmarPublicacao(mensagem.MessageId);
        _logger.LogInformation("Mensagem {MessageId} da Saga {SagaId} publicada. CorrelationId={CorrelationId}",
            mensagem.MessageId, mensagem.SagaId, mensagem.CorrelationId);
    }

    private async Task ReceberEProcessar(CancellationToken token)
    {
        foreach (var recebida in await _transport.Receber(_settings.EventosQueue, token))
        {
            using var scope = _scopes.CreateScope();
            var mensagem = MensagemSerializer.Desserializar(recebida.Body);
            using var logScope = CriarEscopo(mensagem);
            _logger.LogInformation("Processando mensagem {MessageId} do tipo {MessageType}. SagaId={SagaId} CorrelationId={CorrelationId}",
                mensagem.MessageId, mensagem.Type, mensagem.SagaId, mensagem.CorrelationId);
            scope.ServiceProvider.GetRequiredService<IntegracaoCleanController>().Processar(mensagem);
            // Confirmacao continua depois da transacao local; falhas permitem reentrega/DLQ.
            await _transport.Confirmar(_settings.EventosQueue, recebida.ReceiptHandle, token);
            _logger.LogInformation("Mensagem {MessageId} processada", mensagem.MessageId);
        }
    }

    private IDisposable? CriarEscopo(MensagemIntegracao mensagem) => _logger.BeginScope(new Dictionary<string, object>
    {
        ["SagaId"] = mensagem.SagaId,
        ["MessageId"] = mensagem.MessageId,
        ["MessageType"] = mensagem.Type,
        ["CorrelationId"] = mensagem.CorrelationId
    });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopes.CreateScope())
                {
                    var outbox = scope.ServiceProvider.GetRequiredService<IOutboxGateway>();
                    foreach (var mensagem in outbox.ObterPendentes(10))
                    {
                        await PublicarMensagem(mensagem, outbox, stoppingToken);
                    }
                }
                await ReceberEProcessar(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Integracao interrompida; mensagem nao confirmada sera tentada novamente ou enviada a DLQ");
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
