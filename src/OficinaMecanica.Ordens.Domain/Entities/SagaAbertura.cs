namespace OficinaMecanica.Ordens.Domain.Entities;

public class SagaAbertura
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid PedidoRegistroId { get; set; }
    public Guid? PedidoCancelamentoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public StatusSagaAbertura Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime AtualizadaEm { get; set; }
    public long Versao { get; set; }

    public static SagaAbertura Iniciar(Guid ordemServicoId, string? correlationId)
    {
        var id = Guid.NewGuid();
        return new SagaAbertura
        {
            Id = id,
            OrdemServicoId = ordemServicoId,
            PedidoRegistroId = Guid.NewGuid(),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? id.ToString() : correlationId,
            Status = StatusSagaAbertura.AguardandoRegistro,
            CriadaEm = DateTime.UtcNow,
            AtualizadaEm = DateTime.UtcNow,
            Versao = 1
        };
    }

    public bool SolicitarCancelamento(string motivo)
    {
        if (Status == StatusSagaAbertura.CompensacaoPendente || Status == StatusSagaAbertura.Compensada) return false;
        if (Status is not (StatusSagaAbertura.AguardandoRegistro or StatusSagaAbertura.RegistroConfirmado))
            throw new InvalidOperationException("Esta Saga nao permite cancelamento automatico da entrada.");
        Status = StatusSagaAbertura.CompensacaoPendente;
        PedidoCancelamentoId = Guid.NewGuid();
        Motivo = motivo;
        Alterada();
        return true;
    }

    public void ConfirmarRegistro(Guid execucaoId)
    {
        if (Status is StatusSagaAbertura.Compensada or StatusSagaAbertura.RegistroRecusado
            or StatusSagaAbertura.FalhaRequerIntervencao) return;
        if (ExecucaoId.HasValue && ExecucaoId != execucaoId)
            throw new InvalidOperationException("A confirmacao aponta para outra execucao.");
        ExecucaoId = execucaoId;
        if (Status == StatusSagaAbertura.AguardandoRegistro) Status = StatusSagaAbertura.RegistroConfirmado;
        Alterada();
    }

    public void RecusarRegistro(string motivo, bool requerIntervencao = false)
    {
        if (Status != StatusSagaAbertura.AguardandoRegistro) return;
        Status = requerIntervencao ? StatusSagaAbertura.FalhaRequerIntervencao : StatusSagaAbertura.RegistroRecusado;
        Motivo = motivo;
        Alterada();
    }

    public void ConcluirCancelamento(bool aceito, string? motivo)
    {
        if (Status != StatusSagaAbertura.CompensacaoPendente) return;
        Status = aceito ? StatusSagaAbertura.Compensada : StatusSagaAbertura.FalhaRequerIntervencao;
        if (!aceito) Motivo = motivo;
        Alterada();
    }

    private void Alterada() { Versao++; AtualizadaEm = DateTime.UtcNow; }
}
