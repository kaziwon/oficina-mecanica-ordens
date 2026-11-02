using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class ClienteGateway : IClienteGateway
{
    private readonly IClienteDataSource _clienteDataSource;

    public ClienteGateway(IClienteDataSource clienteDataSource)
    {
        _clienteDataSource = clienteDataSource;
    }

    public List<Cliente> ObterTodos()
    {
        return _clienteDataSource.ObterTodos();
    }

    public Cliente? ObterPorId(Guid id)
    {
        return _clienteDataSource.ObterPorId(id);
    }

    public Cliente? ObterPorCpfCnpj(string cpfCnpj)
    {
        return _clienteDataSource.ObterPorCpfCnpj(cpfCnpj);
    }

    public bool ExistePorCpfCnpj(string cpfCnpj)
    {
        return _clienteDataSource.ExistePorCpfCnpj(cpfCnpj);
    }

    public bool ExistePorCpfCnpjExcetoId(string cpfCnpj, Guid id)
    {
        return _clienteDataSource.ExistePorCpfCnpjExcetoId(cpfCnpj, id);
    }

    public Cliente Adicionar(Cliente cliente)
    {
        return _clienteDataSource.Adicionar(cliente);
    }

    public Cliente? Atualizar(Cliente cliente)
    {
        return _clienteDataSource.Atualizar(cliente);
    }

    public bool Remover(Guid id)
    {
        return _clienteDataSource.Remover(id);
    }
}
