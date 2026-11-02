using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class OrdemServicoResponseDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid VeiculoId { get; set; }
    public string DescricaoSolicitacao { get; set; } = string.Empty;
    public StatusOrdemServico Status { get; set; }
    public DateTime CriadaEm { get; set; }
    public List<HistoricoStatusResponseDto> Historico { get; set; } = [];
}
