using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class EfVeiculoDataSource : IVeiculoDataSource
{
    private readonly AppDbContext _context;

    public EfVeiculoDataSource(AppDbContext context)
    {
        _context = context;
    }

    public Veiculo Adicionar(Veiculo veiculo)
    {
        _context.Veiculos.Add(veiculo);
        _context.SaveChanges();

        return veiculo;
    }

    public Veiculo? Atualizar(Veiculo veiculo)
    {
        var veiculoExistente = _context.Veiculos.FirstOrDefault(v => v.Id == veiculo.Id && v.Ativo);

        if (veiculoExistente is null)
        {
            return null;
        }

        if (veiculoExistente.ClienteId != veiculo.ClienteId &&
            _context.OrdensServico.Any(ordem => ordem.VeiculoId == veiculo.Id))
        {
            throw new InvalidOperationException("Um veiculo com OS vinculada nao pode trocar de cliente.");
        }

        veiculoExistente.ClienteId = veiculo.ClienteId;
        veiculoExistente.Placa = veiculo.Placa;
        veiculoExistente.Marca = veiculo.Marca;
        veiculoExistente.Modelo = veiculo.Modelo;
        veiculoExistente.Ano = veiculo.Ano;

        _context.SaveChanges();

        return veiculoExistente;
    }

    public bool ClienteExiste(Guid clienteId)
    {
        return _context.Clientes.Any(cliente => cliente.Id == clienteId && cliente.Ativo);
    }

    public bool ExistePorPlaca(string placa)
    {
        return _context.Veiculos.Any(veiculo => veiculo.Placa == NormalizarPlaca(placa));
    }

    public bool ExistePorPlacaExcetoId(string placa, Guid id)
    {
        var placaNormalizada = NormalizarPlaca(placa);
        return _context.Veiculos.Any(veiculo => veiculo.Placa == placaNormalizada && veiculo.Id != id);
    }

    public Veiculo? ObterPorId(Guid id)
    {
        return _context.Veiculos.AsNoTracking().FirstOrDefault(veiculo => veiculo.Id == id && veiculo.Ativo);
    }

    public List<Veiculo> ObterTodos()
    {
        return _context.Veiculos.AsNoTracking().Where(veiculo => veiculo.Ativo).ToList();
    }

    public bool Remover(Guid id)
    {
        var veiculo = _context.Veiculos.FirstOrDefault(v => v.Id == id && v.Ativo);

        if (veiculo is null)
        {
            return false;
        }

        veiculo.Ativo = false;
        _context.SaveChanges();

        return true;
    }

    private static string NormalizarPlaca(string placa)
    {
        return placa.Trim().ToUpperInvariant().Replace("-", "");
    }
}
