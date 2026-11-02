using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class EfClienteDataSource : IClienteDataSource
{
    private readonly AppDbContext _context;

    public EfClienteDataSource(AppDbContext context)
    {
        _context = context;
    }

    public Cliente Adicionar(Cliente cliente)
    {
        _context.Clientes.Add(cliente);
        _context.SaveChanges();

        return cliente;
    }

    public Cliente? Atualizar(Cliente cliente)
    {
        var clienteExistente = _context.Clientes.FirstOrDefault(c => c.Id == cliente.Id && c.Ativo);

        if (clienteExistente is null)
        {
            return null;
        }

        clienteExistente.Nome = cliente.Nome;
        clienteExistente.CpfCnpj = cliente.CpfCnpj;
        clienteExistente.Email = cliente.Email;
        clienteExistente.Telefone = cliente.Telefone;

        _context.SaveChanges();

        return clienteExistente;
    }

    public bool ExistePorCpfCnpj(string cpfCnpj)
    {
        return _context.Clientes.Any(cliente => cliente.CpfCnpj == cpfCnpj);
    }

    public bool ExistePorCpfCnpjExcetoId(string cpfCnpj, Guid id)
    {
        return _context.Clientes.Any(cliente => cliente.CpfCnpj == cpfCnpj && cliente.Id != id);
    }

    public Cliente? ObterPorId(Guid id)
    {
        return _context.Clientes.AsNoTracking().FirstOrDefault(cliente => cliente.Id == id && cliente.Ativo);
    }

    public Cliente? ObterPorCpfCnpj(string cpfCnpj)
    {
        return _context.Clientes.AsNoTracking().FirstOrDefault(cliente => cliente.CpfCnpj == cpfCnpj && cliente.Ativo);
    }

    public List<Cliente> ObterTodos()
    {
        return _context.Clientes.AsNoTracking().Where(cliente => cliente.Ativo).ToList();
    }

    public bool Remover(Guid id)
    {
        var cliente = _context.Clientes.FirstOrDefault(c => c.Id == id && c.Ativo);

        if (cliente is null)
        {
            return false;
        }

        cliente.Ativo = false;
        _context.SaveChanges();

        return true;
    }
}
