namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IAdminUserGateway
{
    bool CredenciaisValidas(string username, string password);
}
