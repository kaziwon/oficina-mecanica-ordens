namespace OficinaMecanica.Ordens.Application.Messaging;

public class DadosMensagem
{
    public Guid OrdemServicoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public string? DescricaoSolicitacao { get; set; }
    public string? Codigo { get; set; }
    public string? Motivo { get; set; }
}
