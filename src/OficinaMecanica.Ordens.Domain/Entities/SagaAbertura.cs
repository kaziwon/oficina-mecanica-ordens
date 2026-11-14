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
    public Guid? EventoRespostaOrcamentoId { get; set; }
    public bool? OrcamentoAprovado { get; set; }
    public DateTime? OrcamentoCriadoEm { get; set; }
    public DateTime? OrcamentoRespondidoEm { get; set; }
    public Guid? PedidoCancelamentoPorRecusaId { get; set; }
    public Guid? PedidoPagamentoId { get; set; }
    public Guid? PagamentoId { get; set; }
    public string? PagamentoProvedorId { get; set; }
    public DateTime? PagamentoConfirmadoEm { get; set; }
    public Guid? EventoPagamentoConfirmadoId { get; set; }
    public Guid? PedidoLiberacaoExecucaoId { get; set; }
    public DateTime? ExecucaoLiberadaEm { get; set; }
    public DateTime? ReparoIniciadoEm { get; set; }
    public DateTime? ReparoConcluidoEm { get; set; }
    public Guid? PedidoCompensacaoId { get; set; }
    public StatusSagaAbertura? StatusAntesCompensacao { get; set; }
    public string? MotivoCompensacao { get; set; }
    public DateTime? CompensacaoSolicitadaEm { get; set; }
    public DateTime? CompensacaoRecusadaEm { get; set; }
    public DateTime? ExecucaoCanceladaEm { get; set; }
    public Guid? PedidoEstornoId { get; set; }
    public string? EstornoProvedorId { get; set; }
    public DateTime? EstornadoEm { get; set; }
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
        if (Status is StatusSagaAbertura.Compensada or StatusSagaAbertura.RegistroRecusado
            or StatusSagaAbertura.OrcamentoRecusado) return false;
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

    public bool ResponderOrcamento(Guid eventoId, bool aprovado, DateTime criadoEm, DateTime respondidoEm, string? motivo)
    {
        if (EventoRespostaOrcamentoId.HasValue)
        {
            if (EventoRespostaOrcamentoId != eventoId || OrcamentoAprovado != aprovado)
                throw new InvalidOperationException("O orcamento ja recebeu outra resposta nesta Saga.");
            return false;
        }
        if (Status != StatusSagaAbertura.OrcamentoGerado)
            throw new InvalidOperationException("A Saga nao esta aguardando resposta do orcamento.");
        EventoRespostaOrcamentoId = eventoId;
        OrcamentoAprovado = aprovado;
        OrcamentoCriadoEm = criadoEm;
        OrcamentoRespondidoEm = respondidoEm;
        Motivo = motivo;
        Status = aprovado ? StatusSagaAbertura.AguardandoPagamento : StatusSagaAbertura.CancelamentoPorRecusaPendente;
        if (!aprovado) PedidoCancelamentoPorRecusaId = Guid.NewGuid();
        else PedidoPagamentoId = Guid.NewGuid();
        Alterada();
        return true;
    }

    public void ConcluirCancelamentoPorRecusa(bool aceito, string? motivo)
    {
        if (Status != StatusSagaAbertura.CancelamentoPorRecusaPendente) return;
        Status = aceito ? StatusSagaAbertura.OrcamentoRecusado : StatusSagaAbertura.FalhaRequerIntervencao;
        if (!aceito) Motivo = motivo;
        Alterada();
    }

    public bool RegistrarResultadoPagamento(Guid pagamentoId, string? provedorId, bool confirmado, DateTime ocorridoEm, string? motivo)
    {
        if (PagamentoId.HasValue && PagamentoId != pagamentoId)
            throw new InvalidOperationException("O resultado pertence a outro pagamento.");
        if (PagamentoProvedorId is not null)
        {
            // Uma falha tecnica atrasada nao substitui o resultado financeiro ja conhecido.
            if (!confirmado && provedorId is null) return false;
            if (PagamentoProvedorId == provedorId && confirmado == PagamentoConfirmadoEm.HasValue) return false;
            throw new InvalidOperationException("O resultado financeiro conhecido nao pode ser substituido.");
        }
        var reconciliacao = Status == StatusSagaAbertura.FalhaRequerIntervencao && PagamentoId == pagamentoId;
        if ((!reconciliacao && Status != StatusSagaAbertura.AguardandoPagamento) || OrcamentoAprovado != true)
            throw new InvalidOperationException("A Saga nao aguarda resultado de pagamento.");
        if (reconciliacao && !confirmado && provedorId is null) return false;
        PagamentoId = pagamentoId; PagamentoProvedorId = provedorId;
        Status = confirmado ? StatusSagaAbertura.PagamentoConfirmado : StatusSagaAbertura.FalhaRequerIntervencao;
        if (confirmado) { PagamentoConfirmadoEm = ocorridoEm; Motivo = null; }
        else Motivo = motivo;
        Alterada();
        return true;
    }

    public void SolicitarLiberacaoExecucao(Guid confirmacaoPagamentoId)
    {
        if (Status != StatusSagaAbertura.PagamentoConfirmado || !PagamentoConfirmadoEm.HasValue
            || OrcamentoAprovado != true || !PagamentoId.HasValue || !OrcamentoId.HasValue
            || !ExecucaoId.HasValue || !ValorOrcamento.HasValue || ValorOrcamento <= 0
            || string.IsNullOrWhiteSpace(PagamentoProvedorId)
            || confirmacaoPagamentoId == Guid.Empty || PedidoLiberacaoExecucaoId.HasValue)
            throw new InvalidOperationException("A Saga nao permite solicitar liberacao de execucao.");
        EventoPagamentoConfirmadoId = confirmacaoPagamentoId;
        PedidoLiberacaoExecucaoId = Guid.NewGuid();
        Status = StatusSagaAbertura.LiberacaoExecucaoPendente;
        Alterada();
    }

    public bool RegistrarRespostaLiberacao(bool aceita, DateTime ocorridoEm, string? motivo)
    {
        if (PedidoCompensacaoId.HasValue && !CompensacaoRecusadaEm.HasValue)
        {
            if (aceita && !ExecucaoLiberadaEm.HasValue) { ExecucaoLiberadaEm = ocorridoEm; Alterada(); }
            return false;
        }
        if (ExecucaoLiberadaEm.HasValue && aceita) return false;
        if (Status != StatusSagaAbertura.LiberacaoExecucaoPendente)
            throw new InvalidOperationException("A Saga nao aguarda esta resposta de liberacao.");
        Status = aceita ? StatusSagaAbertura.ExecucaoLiberada : StatusSagaAbertura.FalhaRequerIntervencao;
        if (aceita) ExecucaoLiberadaEm = ocorridoEm;
        else Motivo = motivo;
        Alterada();
        return true;
    }

    public bool RegistrarInicioReparo(DateTime iniciadoEm)
    {
        if (Status == StatusSagaAbertura.CancelamentoPagoPendente)
            throw new InvalidOperationException("Aguarde a resposta do cancelamento para reprocessar o inicio.");
        if (ExecucaoCanceladaEm.HasValue)
        {
            if (iniciadoEm > ExecucaoCanceladaEm.Value || (ReparoIniciadoEm.HasValue && ReparoIniciadoEm != iniciadoEm))
                throw new InvalidOperationException("Inicio diverge do reparo cancelado.");
            if (!ReparoIniciadoEm.HasValue) { ReparoIniciadoEm = iniciadoEm; Alterada(); }
            return false;
        }
        if (ReparoIniciadoEm.HasValue)
        {
            if (ReparoIniciadoEm.Value == iniciadoEm) return false;
            throw new InvalidOperationException("O inicio do reparo ja foi registrado com outra data.");
        }
        if (Status != StatusSagaAbertura.ExecucaoLiberada || !ExecucaoLiberadaEm.HasValue
            || !PagamentoConfirmadoEm.HasValue || iniciadoEm < ExecucaoLiberadaEm.Value)
            throw new InvalidOperationException("A Saga nao possui liberacao confirmada para iniciar o reparo.");
        ReparoIniciadoEm = iniciadoEm;
        Status = StatusSagaAbertura.EmExecucao;
        Alterada();
        return true;
    }

    public bool RegistrarConclusaoReparo(DateTime iniciadoEm, DateTime concluidoEm)
    {
        if (ReparoConcluidoEm.HasValue)
        {
            if (ReparoIniciadoEm == iniciadoEm && ReparoConcluidoEm == concluidoEm) return false;
            throw new InvalidOperationException("A conclusao do reparo ja foi registrada com outras datas.");
        }
        if (Status != StatusSagaAbertura.EmExecucao || !ReparoIniciadoEm.HasValue
            || ReparoIniciadoEm != iniciadoEm || concluidoEm < iniciadoEm)
            throw new InvalidOperationException("A Saga nao possui inicio confirmado para esta conclusao.");
        ReparoConcluidoEm = concluidoEm;
        Status = StatusSagaAbertura.Finalizada;
        Alterada();
        return true;
    }

    public bool SolicitarCompensacaoPaga(string motivo)
    {
        if (PedidoCompensacaoId.HasValue)
        {
            if (MotivoCompensacao != motivo) throw new InvalidOperationException("O cancelamento ja foi solicitado com outro motivo.");
            return false;
        }
        if (!PagamentoConfirmadoEm.HasValue || !PagamentoId.HasValue || !PedidoLiberacaoExecucaoId.HasValue
            || !ExecucaoId.HasValue || !OrcamentoId.HasValue || ReparoConcluidoEm.HasValue
            || Status is not (StatusSagaAbertura.LiberacaoExecucaoPendente or StatusSagaAbertura.ExecucaoLiberada
                or StatusSagaAbertura.EmExecucao or StatusSagaAbertura.FalhaRequerIntervencao))
            throw new InvalidOperationException("Somente reparo pago, ainda nao concluido, permite esta compensacao.");
        StatusAntesCompensacao = Status; Status = StatusSagaAbertura.CancelamentoPagoPendente;
        PedidoCompensacaoId = Guid.NewGuid(); MotivoCompensacao = motivo; CompensacaoSolicitadaEm = DateTime.UtcNow;
        Alterada(); return true;
    }

    public bool RegistrarCancelamentoPago(bool aceito, DateTime ocorridoEm, string? motivo)
    {
        if (ExecucaoCanceladaEm.HasValue)
        {
            if (aceito && ExecucaoCanceladaEm == ocorridoEm) return false;
            throw new InvalidOperationException("O cancelamento ja recebeu outra resposta.");
        }
        if (CompensacaoRecusadaEm.HasValue && !aceito) return false;
        if (Status != StatusSagaAbertura.CancelamentoPagoPendente || !StatusAntesCompensacao.HasValue)
            throw new InvalidOperationException("A Saga nao aguarda esta resposta de cancelamento.");
        if (aceito)
        {
            ExecucaoCanceladaEm = ocorridoEm; PedidoEstornoId = Guid.NewGuid(); Status = StatusSagaAbertura.EstornoPendente;
        }
        else
        {
            Status = ReparoIniciadoEm.HasValue ? StatusSagaAbertura.EmExecucao
                : ExecucaoLiberadaEm.HasValue ? StatusSagaAbertura.ExecucaoLiberada : StatusAntesCompensacao.Value;
            CompensacaoRecusadaEm = ocorridoEm; Motivo = motivo;
        }
        Alterada(); return true;
    }

    public bool RegistrarEstorno(bool confirmado, string? provedorId, DateTime ocorridoEm, string? motivo)
    {
        if (EstornoProvedorId is not null)
        {
            if (!confirmado && provedorId is null) return false;
            if (EstornoProvedorId == provedorId && confirmado == EstornadoEm.HasValue) return false;
            throw new InvalidOperationException("O resultado conhecido do estorno nao pode ser substituido.");
        }
        var reconciliacao = Status == StatusSagaAbertura.EstornoRequerIntervencao;
        if ((!reconciliacao && Status != StatusSagaAbertura.EstornoPendente) || !ExecucaoCanceladaEm.HasValue)
            throw new InvalidOperationException("A Saga nao aguarda este resultado de estorno.");
        if (reconciliacao && !confirmado && provedorId is null) return false;
        Status = confirmado ? StatusSagaAbertura.CompensadaAposPagamento : StatusSagaAbertura.EstornoRequerIntervencao;
        EstornoProvedorId = provedorId; if (confirmado) { EstornadoEm = ocorridoEm; Motivo = null; } else Motivo = motivo;
        Alterada(); return true;
    }

    private void Alterada() { Versao++; AtualizadaEm = DateTime.UtcNow; }
}
