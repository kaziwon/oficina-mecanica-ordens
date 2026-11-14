namespace OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

public class OrcamentoClienteOutput
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid ExecucaoId { get; set; }
    public string Diagnostico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? RespondidoEm { get; set; }
    public string? MotivoRecusa { get; set; }
    public List<ItemOrcamentoClienteOutput> Itens { get; set; } = [];
}
