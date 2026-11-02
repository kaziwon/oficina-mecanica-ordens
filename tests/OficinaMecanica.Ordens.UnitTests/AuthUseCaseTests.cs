using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.UseCases.Auth;

namespace OficinaMecanica.Ordens.UnitTests;

public class AuthUseCaseTests
{
    [Fact]
    public void Login_DeveRetornarNull_QuandoCredenciaisForemInvalidas()
    {
        var useCase = CriarUseCase();

        var response = useCase.Executar(new LoginInput("admin", "senha-errada"));

        Assert.Null(response);
    }

    [Fact]
    public void Login_DeveRetornarTokenValido_QuandoCredenciaisForemValidas()
    {
        var useCase = CriarUseCase();

        var response = useCase.Executar(new LoginInput("admin", "Admin@123"));

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("token-admin-Admin", response.Token);
    }

    private static LoginUseCase CriarUseCase()
    {
        return new LoginUseCase(new AdminUserGatewayFake(), new TokenGatewayFake());
    }

    private class AdminUserGatewayFake : IAdminUserGateway
    {
        public bool CredenciaisValidas(string username, string password)
        {
            return username == "admin" && password == "Admin@123";
        }
    }

    private class TokenGatewayFake : ITokenGateway
    {
        public TokenGerado GerarToken(string username, string role)
        {
            return new TokenGerado
            {
                Token = $"token-{username}-{role}",
                ExpiresAt = DateTime.UtcNow.AddHours(2)
            };
        }
    }
}
