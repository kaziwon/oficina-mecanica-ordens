namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IClienteTokenGateway
{
    TokenGerado GerarToken(Guid clienteId, string nome);
}
