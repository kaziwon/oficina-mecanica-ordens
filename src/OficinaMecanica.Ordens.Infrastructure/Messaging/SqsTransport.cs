using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Infrastructure.Settings;

namespace OficinaMecanica.Ordens.Infrastructure.Messaging;

public sealed class SqsTransport : IDisposable
{
    private readonly IAmazonSQS _client;
    private readonly Dictionary<string, string> _urls = [];
    public SqsTransport(IOptions<MessagingSettings> options)
    {
        var settings = options.Value;
        if (settings.UsarAws)
            _client = new AmazonSQSClient(RegionEndpoint.GetBySystemName(settings.Region));
        else
        {
            if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Configure um endpoint SQS local valido.");
            _client = new AmazonSQSClient(new BasicAWSCredentials("local", "local"), new AmazonSQSConfig
            {
                ServiceURL = settings.Endpoint,
                AuthenticationRegion = settings.Region,
                MaxErrorRetry = 1,
                Timeout = TimeSpan.FromSeconds(15)
            });
        }
    }

    private async Task<string> Url(string nome, CancellationToken token)
    {
        if (!_urls.TryGetValue(nome, out var url))
        {
            url = (await _client.GetQueueUrlAsync(nome, token)).QueueUrl;
            _urls[nome] = url;
        }
        return url;
    }

    public async Task Enviar(string fila, string body, CancellationToken token)
        => await _client.SendMessageAsync(new SendMessageRequest { QueueUrl = await Url(fila, token), MessageBody = body }, token);

    public async Task<List<Message>> Receber(string fila, CancellationToken token)
    {
        var response = await _client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = await Url(fila, token),
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 2,
            VisibilityTimeout = 30
        }, token);
        return response.Messages ?? [];
    }

    public async Task Confirmar(string fila, string receiptHandle, CancellationToken token)
        => await _client.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = await Url(fila, token),
            ReceiptHandle = receiptHandle
        }, token);

    public void Dispose() => _client.Dispose();
}
