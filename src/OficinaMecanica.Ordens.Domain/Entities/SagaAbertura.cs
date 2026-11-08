namespace OficinaMecanica.Ordens.Domain.Entities;

public class SagaAbertura
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid PedidoRegistroId { get; set; }
    public Guid? PedidoCancelamentoId { get; set; }
    public Guid? ExecucaoId { get; set; }
    public Guid? EventoDiagnosticoConcluidoId { get; set; }
    public Guid? PedidoOrcamentoId { get; set; }
    public Guid? OrcamentoId { get; set; }
    public decimal? ValorOrcamento { get; set; }
    public DateTime? DiagnosticoIniciadoEm { get; set; }
    public DateTime? DiagnosticoConcluidoEm { get; set; }
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

    public bool RegistrarDiagnostico(Guid execucaoId, DateTime inicio)
    {
        if (Status is StatusSagaAbertura.Compensada or StatusSagaAbertura.RegistroRecusado) return false;
        if ((ExecucaoId.HasValue && ExecucaoId != execucaoId)
            || (DiagnosticoIniciadoEm.HasValue && DiagnosticoIniciadoEm != inicio))
            throw new InvalidOperationException("Diagnostico diverge da execucao conhecida.");
        ExecucaoId = execucaoId;
        DiagnosticoIniciadoEm = inicio;
        if (Status is StatusSagaAbertura.AguardandoRegistro or StatusSagaAbertura.RegistroConfirmado)
            Status = StatusSagaAbertura.EmDiagnostico;
        Alterada();
        return true;
    }

    public bool SolicitarOrcamento(Guid eventoId, Guid execucaoId, DateTime inicio, DateTime fim)
    {
        if (EventoDiagnosticoConcluidoId.HasValue)
        {
            if (EventoDiagnosticoConcluidoId != eventoId)
                throw new InvalidOperationException("Outro diagnostico ja foi concluido nesta Saga.");
            return false;
        }
        if (Status == StatusSagaAbertura.CompensacaoPendente)
            throw new InvalidOperationException("Aguarde a resposta da compensacao antes de processar o diagnostico.");
        if (!RegistrarDiagnostico(execucaoId, inicio)) return false;
        EventoDiagnosticoConcluidoId = eventoId;
        DiagnosticoConcluidoEm = fim;
        if (Status == StatusSagaAbertura.FalhaRequerIntervencao)
        {
            Alterada();
            return false;
        }
        PedidoOrcamentoId = Guid.NewGuid();
        Status = StatusSagaAbertura.OrcamentoPendente;
        Alterada();
        return true;
    }

    public void ConfirmarOrcamento(Guid orcamentoId, decimal valor)
    {
        if (Status != StatusSagaAbertura.OrcamentoPendente) return;
        OrcamentoId = orcamentoId;
        ValorOrcamento = valor;
        Status = StatusSagaAbertura.OrcamentoGerado;
        Alterada();
    }

    public void RecusarOrcamento(string motivo)
    {
        if (Status != StatusSagaAbertura.OrcamentoPendente) return;
        Status = StatusSagaAbertura.FalhaRequerIntervencao;
        Motivo = motivo;
        Alterada();
    }

    private void Alterada() { Versao++; AtualizadaEm = DateTime.UtcNow; }
}
