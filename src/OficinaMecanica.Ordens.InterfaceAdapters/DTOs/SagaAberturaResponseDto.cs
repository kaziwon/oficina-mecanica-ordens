using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class SagaAberturaResponseDto
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public StatusSagaAbertura Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime AtualizadaEm { get; set; }
}
