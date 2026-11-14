using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.UseCases.Auth;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class AuthClienteCleanController(LoginClienteUseCase login)
{
    public LoginClienteResponseDto? Login(LoginClienteRequestDto request)
    {
        var output = login.Executar(new LoginClienteInput(request.Cpf));
        return output is null ? null : LoginClientePresenter.Apresentar(output);
    }
}
