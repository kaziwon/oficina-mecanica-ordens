namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Models;

public class MensagemInbox
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public DateTime ProcessadaEm { get; set; }
}
