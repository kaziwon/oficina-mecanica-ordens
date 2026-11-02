using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class VeiculoGateway : IVeiculoGateway
{
    private readonly IVeiculoDataSource _veiculoDataSource;

    public VeiculoGateway(IVeiculoDataSource veiculoDataSource)
    {
        _veiculoDataSource = veiculoDataSource;
    }

    public List<Veiculo> ObterTodos()
    {
        return _veiculoDataSource.ObterTodos();
    }

    public Veiculo? ObterPorId(Guid id)
    {
        return _veiculoDataSource.ObterPorId(id);
    }

    public bool ExistePorPlaca(string placa)
    {
        return _veiculoDataSource.ExistePorPlaca(placa);
    }

    public bool ExistePorPlacaExcetoId(string placa, Guid id)
    {
        return _veiculoDataSource.ExistePorPlacaExcetoId(placa, id);
    }

    public bool ClienteExiste(Guid clienteId)
    {
        return _veiculoDataSource.ClienteExiste(clienteId);
    }

    public Veiculo Adicionar(Veiculo veiculo)
    {
        return _veiculoDataSource.Adicionar(veiculo);
    }

    public Veiculo? Atualizar(Veiculo veiculo)
    {
        return _veiculoDataSource.Atualizar(veiculo);
    }

    public bool Remover(Guid id)
    {
        return _veiculoDataSource.Remover(id);
    }
}
