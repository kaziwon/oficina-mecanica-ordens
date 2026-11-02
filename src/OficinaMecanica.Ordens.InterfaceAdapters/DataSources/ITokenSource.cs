using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface ITokenSource
{
    TokenGerado GerarToken(string username, string role);
}
