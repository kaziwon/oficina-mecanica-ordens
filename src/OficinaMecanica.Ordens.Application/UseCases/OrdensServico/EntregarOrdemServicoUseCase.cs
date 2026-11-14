using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class EntregarOrdemServicoUseCase
{
    private readonly IOrdemServicoGateway _ordens;
    public EntregarOrdemServicoUseCase(IOrdemServicoGateway ordens) { _ordens = ordens; }

    public OrdemServicoOutput? Executar(Guid id)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var ordem = _ordens.ObterPorId(id);
            if (ordem is null) return null;
            if (!ordem.Entregar()) return OrdemServicoOutputMapper.Mapear(ordem);
            if (_ordens.TentarRegistrarEntrega(ordem))
            {
                var salva = _ordens.ObterPorId(id)
                    ?? throw new InvalidOperationException("Entrega salva; consulte a OS novamente.");
                return OrdemServicoOutputMapper.Mapear(salva);
            }
        }
        throw new InvalidOperationException("A OS foi alterada durante a entrega. Consulte novamente.");
    }
}
