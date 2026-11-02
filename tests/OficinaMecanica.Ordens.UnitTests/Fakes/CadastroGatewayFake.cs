using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public sealed class CadastroGatewayFake : IClienteGateway, IVeiculoGateway
{
    public List<Cliente> Clientes { get; } = [];
    public List<Veiculo> Veiculos { get; } = [];
    public Cliente? ObterPorCpfCnpj(string cpfCnpj) => Clientes.FirstOrDefault(item => item.CpfCnpj == cpfCnpj && item.Ativo);
    Cliente? IClienteGateway.ObterPorId(Guid id) => Clientes.FirstOrDefault(item => item.Id == id && item.Ativo);
    List<Cliente> IClienteGateway.ObterTodos() => Clientes.Where(item => item.Ativo).ToList();
    public bool ExistePorCpfCnpj(string cpfCnpj) => Clientes.Any(item => item.CpfCnpj == cpfCnpj);
    public bool ExistePorCpfCnpjExcetoId(string cpfCnpj, Guid id) => Clientes.Any(item => item.CpfCnpj == cpfCnpj && item.Id != id);
    public Cliente Adicionar(Cliente cliente) { Clientes.Add(cliente); return cliente; }
    public Cliente? Atualizar(Cliente cliente) => throw new NotSupportedException();
    bool IClienteGateway.Remover(Guid id) => throw new NotSupportedException();
    public bool ClienteExiste(Guid id) => Clientes.Any(item => item.Id == id && item.Ativo);
    public bool ExistePorPlaca(string placa) => Veiculos.Any(item => item.Placa == placa);
    public bool ExistePorPlacaExcetoId(string placa, Guid id) => Veiculos.Any(item => item.Placa == placa && item.Id != id);
    Veiculo? IVeiculoGateway.ObterPorId(Guid id) => Veiculos.FirstOrDefault(item => item.Id == id && item.Ativo);
    List<Veiculo> IVeiculoGateway.ObterTodos() => Veiculos.Where(item => item.Ativo).ToList();
    public Veiculo Adicionar(Veiculo veiculo) { Veiculos.Add(veiculo); return veiculo; }
    public Veiculo? Atualizar(Veiculo veiculo) => throw new NotSupportedException();
    bool IVeiculoGateway.Remover(Guid id) => throw new NotSupportedException();
}
