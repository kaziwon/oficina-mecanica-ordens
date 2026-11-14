namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class ItemOrcamentoClienteResponseDto
{
    public string Tipo { get; set; } = string.Empty;
    public Guid ReferenciaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Total { get; set; }
}
