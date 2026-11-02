using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Veiculos;

public class ObterTodosVeiculosUseCase
{
    private readonly IVeiculoGateway _veiculoGateway;

    public ObterTodosVeiculosUseCase(IVeiculoGateway veiculoGateway)
    {
        _veiculoGateway = veiculoGateway;
    }

    public List<VeiculoOutput> Executar()
    {
        return _veiculoGateway.ObterTodos().Select(VeiculoOutputMapper.Mapear).ToList();
    }
}
