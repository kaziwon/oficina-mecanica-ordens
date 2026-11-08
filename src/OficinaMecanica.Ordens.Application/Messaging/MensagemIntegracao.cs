namespace OficinaMecanica.Ordens.Application.Messaging;

public class MensagemIntegracao
{
    public Guid MessageId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid SagaId { get; set; }
    public Guid? CausationId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DadosMensagem Payload { get; set; } = new();
}
