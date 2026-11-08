using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ObterSagaAberturaUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ObterSagaAberturaUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public SagaAberturaOutput? Executar(Guid ordemServicoId)
    {
        var saga = _sagas.ObterPorOrdemServicoId(ordemServicoId);
        return saga is null ? null : SagaAberturaOutputMapper.Mapear(saga);
    }
}
