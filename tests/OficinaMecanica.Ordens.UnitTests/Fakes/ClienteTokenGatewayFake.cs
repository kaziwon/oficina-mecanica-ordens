using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public class ClienteTokenGatewayFake : IClienteTokenGateway
{
    public int Chamadas { get; private set; }
    public Guid? ClienteId { get; private set; }
    public string? Nome { get; private set; }
    public TokenGerado GerarToken(Guid clienteId, string nome)
    { Chamadas++; ClienteId = clienteId; Nome = nome; return new() { Token = "token-cliente", ExpiresAt = DateTime.UtcNow.AddMinutes(30) }; }
}
