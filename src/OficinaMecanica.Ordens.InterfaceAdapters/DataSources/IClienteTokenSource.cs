using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IClienteTokenSource
{
    TokenGerado GerarToken(Guid clienteId, string nome);
}
