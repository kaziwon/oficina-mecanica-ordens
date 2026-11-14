using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class SagaAberturaResponseDto
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public Guid? OrcamentoId { get; set; }
    public decimal? ValorOrcamento { get; set; }
    public bool? OrcamentoAprovado { get; set; }
    public DateTime? OrcamentoRespondidoEm { get; set; }
    public Guid? PagamentoId { get; set; }
    public string? PagamentoProvedorId { get; set; }
    public DateTime? PagamentoConfirmadoEm { get; set; }
    public DateTime? ExecucaoLiberadaEm { get; set; }
    public DateTime? ReparoIniciadoEm { get; set; }
    public DateTime? ReparoConcluidoEm { get; set; }
    public Guid? PedidoCompensacaoId { get; set; }
    public string? MotivoCompensacao { get; set; }
    public DateTime? CompensacaoRecusadaEm { get; set; }
    public DateTime? ExecucaoCanceladaEm { get; set; }
    public Guid? EstornoId { get; set; }
    public string? EstornoProvedorId { get; set; }
    public DateTime? EstornadoEm { get; set; }
    public DateTime? DiagnosticoIniciadoEm { get; set; }
    public DateTime? DiagnosticoConcluidoEm { get; set; }
    public StatusSagaAbertura Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime AtualizadaEm { get; set; }
}
