using System.ComponentModel.DataAnnotations;

namespace OficinaMecanica.Ordens.Controllers.DTOs;

public class OrdemServicoRequestDto
{
    public string? CpfCnpj { get; set; }
    public Guid? VeiculoId { get; set; }
    public ClienteRequestDto? Cliente { get; set; }
    public OrdemServicoVeiculoRequestDto? Veiculo { get; set; }

    [Required, MaxLength(1000)]
    public string DescricaoSolicitacao { get; set; } = string.Empty;
}
