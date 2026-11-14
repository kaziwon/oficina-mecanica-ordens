using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public class ClienteGatewayFake : IClienteGateway
{
    public List<Cliente> Clientes { get; } = [];
    public List<Cliente> ObterTodos() => Clientes.Where(item => item.Ativo).ToList();
    public Cliente? ObterPorId(Guid id) => Clientes.SingleOrDefault(item => item.Id == id && item.Ativo);
    public Cliente? ObterPorCpfCnpj(string documento) => Clientes.SingleOrDefault(item => item.CpfCnpj == documento && item.Ativo);
    public bool ExistePorCpfCnpj(string documento) => Clientes.Any(item => item.CpfCnpj == documento);
    public bool ExistePorCpfCnpjExcetoId(string documento, Guid id) => Clientes.Any(item => item.CpfCnpj == documento && item.Id != id);
    public Cliente Adicionar(Cliente cliente) { Clientes.Add(cliente); return cliente; }
    public Cliente? Atualizar(Cliente cliente) => throw new NotSupportedException();
    public bool Remover(Guid id)
    { var cliente = ObterPorId(id); if (cliente is null) return false; cliente.Ativo = false; return true; }
}
