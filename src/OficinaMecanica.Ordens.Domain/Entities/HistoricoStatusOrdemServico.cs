namespace OficinaMecanica.Ordens.Domain.Entities;

public class HistoricoStatusOrdemServico
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public StatusOrdemServico Status { get; set; }
    public DateTime RegistradoEm { get; set; }
}
