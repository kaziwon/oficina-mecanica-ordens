using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Mappers.Clientes;

internal static class ClienteOutputMapper
{
    public static ClienteOutput Mapear(Cliente cliente)
    {
        return new ClienteOutput
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            CpfCnpj = cliente.CpfCnpj,
            Email = cliente.Email,
            Telefone = cliente.Telefone,
            Ativo = cliente.Ativo
        };
    }
}
