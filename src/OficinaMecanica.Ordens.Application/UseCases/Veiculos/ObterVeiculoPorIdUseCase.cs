using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Veiculos;

public class ObterVeiculoPorIdUseCase
{
    private readonly IVeiculoGateway _veiculoGateway;

    public ObterVeiculoPorIdUseCase(IVeiculoGateway veiculoGateway)
    {
        _veiculoGateway = veiculoGateway;
    }

    public VeiculoOutput? Executar(Guid id)
    {
        var veiculo = _veiculoGateway.ObterPorId(id);

        return veiculo is null ? null : VeiculoOutputMapper.Mapear(veiculo);
    }
}
