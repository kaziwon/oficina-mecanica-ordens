namespace OficinaMecanica.Ordens.Domain.Entities;

public class OrdemServico
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid VeiculoId { get; set; }
    public string DescricaoSolicitacao { get; set; } = string.Empty;
    public StatusOrdemServico Status { get; set; }
    public DateTime CriadaEm { get; set; }
    public Cliente? Cliente { get; set; }
    public Veiculo? Veiculo { get; set; }
    public ICollection<HistoricoStatusOrdemServico> Historico { get; set; } = [];

    public void RegistrarDiagnostico(DateTime inicio)
    {
        if (Status == StatusOrdemServico.Recebida) RegistrarStatus(StatusOrdemServico.EmDiagnostico, inicio);
    }

    public void AguardarAprovacao(DateTime inicio, DateTime orcamentoCriadoEm)
    {
        RegistrarDiagnostico(inicio);
        if (Status == StatusOrdemServico.EmDiagnostico)
            RegistrarStatus(StatusOrdemServico.AguardandoAprovacao, orcamentoCriadoEm);
    }

    public void AguardarPagamento(DateTime inicio, DateTime orcamentoCriadoEm, DateTime respondidoEm)
    {
        AguardarAprovacao(inicio, orcamentoCriadoEm);
        if (Status == StatusOrdemServico.AguardandoAprovacao)
            RegistrarStatus(StatusOrdemServico.AguardandoPagamento, respondidoEm);
    }

    public void ConfirmarPagamento(DateTime confirmadoEm)
    {
        if (Status == StatusOrdemServico.PagamentoConfirmado) return;
        if (Status != StatusOrdemServico.AguardandoPagamento)
            throw new InvalidOperationException("A OS nao aguarda pagamento.");
        RegistrarStatus(StatusOrdemServico.PagamentoConfirmado, confirmadoEm);
    }

    public void AguardarExecucao(DateTime liberadaEm)
    {
        if (Status == StatusOrdemServico.AguardandoExecucao) return;
        if (Status != StatusOrdemServico.PagamentoConfirmado)
            throw new InvalidOperationException("A OS nao possui pagamento confirmado para liberacao.");
        RegistrarStatus(StatusOrdemServico.AguardandoExecucao, liberadaEm);
    }

    public void IniciarReparo(DateTime iniciadoEm)
    {
        if (Status == StatusOrdemServico.EmExecucao) return;
        if (Status != StatusOrdemServico.AguardandoExecucao)
            throw new InvalidOperationException("A OS nao aguarda inicio de execucao.");
        RegistrarStatus(StatusOrdemServico.EmExecucao, iniciadoEm);
    }

    public void ConcluirReparo(DateTime concluidoEm)
    {
        if (Status == StatusOrdemServico.Finalizada) return;
        if (Status != StatusOrdemServico.EmExecucao)
            throw new InvalidOperationException("A OS nao esta em execucao para conclusao.");
        RegistrarStatus(StatusOrdemServico.Finalizada, concluidoEm);
    }

    public void AguardarEstorno(DateTime registradoEm)
    {
        if (Status == StatusOrdemServico.CancelamentoPendente) return;
        if (Status is not (StatusOrdemServico.PagamentoConfirmado or StatusOrdemServico.AguardandoExecucao or StatusOrdemServico.EmExecucao))
            throw new InvalidOperationException("A OS nao permite aguardar estorno neste estado.");
        RegistrarStatus(StatusOrdemServico.CancelamentoPendente, registradoEm);
    }

    public void CancelarAposEstorno(DateTime registradoEm)
    {
        if (Status == StatusOrdemServico.Cancelada) return;
        if (Status != StatusOrdemServico.CancelamentoPendente)
            throw new InvalidOperationException("A OS nao aguarda confirmacao de estorno.");
        RegistrarStatus(StatusOrdemServico.Cancelada, registradoEm);
    }

    public bool Entregar()
    {
        if (Status == StatusOrdemServico.Entregue) return false;
        if (Status != StatusOrdemServico.Finalizada)
            throw new InvalidOperationException("Somente uma OS finalizada pode ser entregue.");
        RegistrarStatus(StatusOrdemServico.Entregue, DateTime.UtcNow);
        return true;
    }

    public void CancelarPorRecusaOrcamento(DateTime registradoEm)
    {
        if (Status == StatusOrdemServico.Cancelada) return;
        if (Status != StatusOrdemServico.AguardandoAprovacao)
            throw new InvalidOperationException("A OS nao pode ser cancelada por recusa de orcamento neste status.");
        RegistrarStatus(StatusOrdemServico.Cancelada, registradoEm);
    }

    private void RegistrarStatus(StatusOrdemServico status, DateTime registradoEm)
    {
        Status = status;
        Historico.Add(new HistoricoStatusOrdemServico
        { Id = Guid.NewGuid(), OrdemServicoId = Id, Status = status, RegistradoEm = registradoEm });
    }

    public void CancelarEntrada()
    {
        if (Status == StatusOrdemServico.Cancelada) return;
        if (Status != StatusOrdemServico.Recebida)
            throw new InvalidOperationException("Uma OS fora de Recebida exige tratamento explicito para cancelamento.");
        Status = StatusOrdemServico.Cancelada;
        Historico.Add(new HistoricoStatusOrdemServico
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = Id,
            Status = Status,
            RegistradoEm = DateTime.UtcNow
        });
    }
}
