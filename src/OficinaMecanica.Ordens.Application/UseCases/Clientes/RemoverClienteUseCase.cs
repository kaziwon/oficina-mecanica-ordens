using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Application.Mappers.Clientes;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Clientes;

public class RemoverClienteUseCase
{
    private readonly IClienteGateway _clienteGateway;

    public RemoverClienteUseCase(IClienteGateway clienteGateway)
    {
        _clienteGateway = clienteGateway;
    }

    public bool Executar(Guid id)
    {
        return _clienteGateway.Remover(id);
    }
}
