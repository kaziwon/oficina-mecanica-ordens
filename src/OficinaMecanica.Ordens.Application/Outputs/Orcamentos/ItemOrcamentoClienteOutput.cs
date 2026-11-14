namespace OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

public class ItemOrcamentoClienteOutput
{
    public string Tipo { get; set; } = string.Empty;
    public Guid ReferenciaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Total { get; set; }
}
