using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class ClientePresenter
{
    public static ClienteResponseDto Apresentar(ClienteOutput cliente)
        => new()
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            CpfCnpj = cliente.CpfCnpj,
            Email = cliente.Email,
            Telefone = cliente.Telefone,
            Ativo = cliente.Ativo
        };
}
