using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Application.Mappers.Clientes;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Clientes;

public class ObterTodosClientesUseCase
{
    private readonly IClienteGateway _clienteGateway;

    public ObterTodosClientesUseCase(IClienteGateway clienteGateway)
    {
        _clienteGateway = clienteGateway;
    }

    public List<ClienteOutput> Executar()
    {
        return _clienteGateway
            .ObterTodos()
            .Select(ClienteOutputMapper.Mapear)
            .ToList();
    }
}
