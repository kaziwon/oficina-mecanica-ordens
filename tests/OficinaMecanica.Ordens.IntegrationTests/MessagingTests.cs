using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Settings;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class MessagingTests
{
    [Fact]
    public void Serializer_PreservaContratoERejeitaEnvelopeIncompletoOuCamposDesconhecidos()
    {
        var mensagem = Mensagem();
        var json = MensagemSerializer.Serializar(mensagem);
        Assert.Contains("\"causationId\":null", json);
        Assert.DoesNotContain("execucaoId", json);
        Assert.Equal(mensagem.MessageId, MensagemSerializer.Desserializar(json).MessageId);
        Assert.Throws<JsonException>(() => MensagemSerializer.Desserializar("{}"));
        Assert.Throws<JsonException>(() => MensagemSerializer.Desserializar(json.Insert(1, "\"externo\":true,")));
    }

    [SqsFact]
    public async Task Sqs_EntregaReentregaEConfirmaSomenteAposPersistencia()
    {
        var endpoint = Environment.GetEnvironmentVariable("SQS_TEST_ENDPOINT")!;
        using var client = new AmazonSQSClient(new BasicAWSCredentials("local", "local"),
            new AmazonSQSConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var nome = "teste-" + Guid.NewGuid().ToString("N");
        var url = (await client.CreateQueueAsync(nome)).QueueUrl;
        try
        {
            using var transport = new SqsTransport(Options.Create(new MessagingSettings { Endpoint = endpoint }));
            var mensagem = Mensagem();
            await transport.Enviar(nome, MensagemSerializer.Serializar(mensagem), CancellationToken.None);
            var primeira = Assert.Single(await transport.Receber(nome, CancellationToken.None));
            Assert.Equal(mensagem.MessageId, MensagemSerializer.Desserializar(primeira.Body).MessageId);
            await client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
            {
                QueueUrl = url,
                ReceiptHandle = primeira.ReceiptHandle,
                VisibilityTimeout = 0
            });
            var repetida = Assert.Single(await transport.Receber(nome, CancellationToken.None));
            Assert.Equal(primeira.Body, repetida.Body);
            await transport.Confirmar(nome, repetida.ReceiptHandle, CancellationToken.None);
            Assert.Empty(await transport.Receber(nome, CancellationToken.None));
        }
        finally { await client.DeleteQueueAsync(url); }
    }

    private static MensagemIntegracao Mensagem() => new()
    {
        MessageId = Guid.NewGuid(),
        Type = "RegistrarExecucao",
        SagaId = Guid.NewGuid(),
        OccurredAt = DateTime.UtcNow,
        CorrelationId = "teste-integracao",
        Payload = new DadosMensagem { OrdemServicoId = Guid.NewGuid(), DescricaoSolicitacao = "Revisao" }
    };
}
