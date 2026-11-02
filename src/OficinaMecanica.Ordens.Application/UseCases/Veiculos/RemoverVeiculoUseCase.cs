using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Veiculos;

public class RemoverVeiculoUseCase
{
    private readonly IVeiculoGateway _veiculoGateway;

    public RemoverVeiculoUseCase(IVeiculoGateway veiculoGateway)
    {
        _veiculoGateway = veiculoGateway;
    }

    public bool Executar(Guid id)
    {
        return _veiculoGateway.Remover(id);
    }
}
