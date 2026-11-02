using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Application.Mappers.Clientes;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Clientes;

public class ObterClientePorIdUseCase
{
    private readonly IClienteGateway _clienteGateway;

    public ObterClientePorIdUseCase(IClienteGateway clienteGateway)
    {
        _clienteGateway = clienteGateway;
    }

    public ClienteOutput? Executar(Guid id)
    {
        var cliente = _clienteGateway.ObterPorId(id);

        return cliente is null ? null : ClienteOutputMapper.Mapear(cliente);
    }
}
