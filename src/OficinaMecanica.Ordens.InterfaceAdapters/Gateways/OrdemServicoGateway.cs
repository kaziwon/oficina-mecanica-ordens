using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class OrdemServicoGateway : IOrdemServicoGateway
{
    private readonly IOrdemServicoDataSource _source;

    public OrdemServicoGateway(IOrdemServicoDataSource source)
    {
        _source = source;
    }

    public OrdemServico Adicionar(OrdemServico ordemServico) => _source.Adicionar(ordemServico);
    public OrdemServico? ObterPorId(Guid id) => _source.ObterPorId(id);
    public List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro)
        => _source.ObterTodas(status, maisNovasPrimeiro);
}
