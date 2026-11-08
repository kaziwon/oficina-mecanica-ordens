namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Models;

public class MensagemOutbox
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CriadaEm { get; set; }
    public DateTime? PublicadaEm { get; set; }
}
