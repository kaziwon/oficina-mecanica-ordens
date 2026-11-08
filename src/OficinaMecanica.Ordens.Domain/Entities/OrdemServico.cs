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
