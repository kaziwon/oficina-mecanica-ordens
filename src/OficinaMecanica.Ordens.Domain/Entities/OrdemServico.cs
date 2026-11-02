namespace OficinaMecanica.Ordens.Domain.Entities;

public class OrdemServico
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid VeiculoId { get; set; }
    public string DescricaoSolicitacao { get; set; } = string.Empty;
    public StatusOrdemServico Status { get; set; }
    public DateTime CriadaEm { get; set; }
    public Cliente? Cliente { get; set; }
    public Veiculo? Veiculo { get; set; }
    public ICollection<HistoricoStatusOrdemServico> Historico { get; set; } = [];
}
