using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class ClienteTokenGateway(IClienteTokenSource source) : IClienteTokenGateway
{
    public TokenGerado GerarToken(Guid clienteId, string nome) => source.GerarToken(clienteId, nome);
}
