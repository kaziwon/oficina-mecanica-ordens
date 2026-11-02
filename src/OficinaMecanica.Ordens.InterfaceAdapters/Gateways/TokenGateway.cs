using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class TokenGateway : ITokenGateway
{
    private readonly ITokenSource _tokenSource;

    public TokenGateway(ITokenSource tokenSource)
    {
        _tokenSource = tokenSource;
    }

    public TokenGerado GerarToken(string username, string role)
    {
        return _tokenSource.GerarToken(username, role);
    }
}
