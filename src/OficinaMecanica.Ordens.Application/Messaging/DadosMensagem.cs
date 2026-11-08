namespace OficinaMecanica.Ordens.Application.Messaging;

public class DadosMensagem
{
    public Guid OrdemServicoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public string? DescricaoSolicitacao { get; set; }
    public string? Codigo { get; set; }
    public string? Motivo { get; set; }
    public string? Diagnostico { get; set; }
    public DateTime? DiagnosticoIniciadoEm { get; set; }
    public DateTime? DiagnosticoConcluidoEm { get; set; }
    public List<ItemMensagem>? Itens { get; set; }
    public Guid? OrcamentoId { get; set; }
    public decimal? ValorTotal { get; set; }
}
