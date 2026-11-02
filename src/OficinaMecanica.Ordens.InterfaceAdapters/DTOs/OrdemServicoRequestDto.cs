namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class OrdemServicoRequestDto
{
    public string? CpfCnpj { get; set; }
    public Guid? VeiculoId { get; set; }
    public ClienteRequestDto? Cliente { get; set; }
    public OrdemServicoVeiculoRequestDto? Veiculo { get; set; }
    public string DescricaoSolicitacao { get; set; } = string.Empty;
}
