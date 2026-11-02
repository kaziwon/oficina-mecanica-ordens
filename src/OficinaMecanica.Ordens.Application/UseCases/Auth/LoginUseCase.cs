using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.Outputs.Auth;
using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.Application.UseCases.Auth;

public class LoginUseCase
{
    private readonly IAdminUserGateway _adminUserGateway;
    private readonly ITokenGateway _tokenGateway;

    public LoginUseCase(IAdminUserGateway adminUserGateway, ITokenGateway tokenGateway)
    {
        _adminUserGateway = adminUserGateway;
        _tokenGateway = tokenGateway;
    }

    public LoginOutput? Executar(LoginInput input)
    {
        if (!_adminUserGateway.CredenciaisValidas(input.Username, input.Password))
        {
            return null;
        }

        var token = _tokenGateway.GerarToken(input.Username, "Admin");

        return new LoginOutput
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt
        };
    }
}
