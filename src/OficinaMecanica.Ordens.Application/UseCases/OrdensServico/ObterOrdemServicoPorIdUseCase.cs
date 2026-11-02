using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ObterOrdemServicoPorIdUseCase
{
    private readonly IOrdemServicoGateway _ordens;

    public ObterOrdemServicoPorIdUseCase(IOrdemServicoGateway ordens)
    {
        _ordens = ordens;
    }

    public OrdemServicoOutput? Executar(Guid id)
    {
        var ordem = _ordens.ObterPorId(id);
        return ordem is null ? null : OrdemServicoOutputMapper.Mapear(ordem);
    }
}
