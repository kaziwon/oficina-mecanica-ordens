namespace OficinaMecanica.Ordens.Application.Gateways;

public interface ITokenGateway
{
    TokenGerado GerarToken(string username, string role);
}
