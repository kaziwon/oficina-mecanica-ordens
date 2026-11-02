using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.Outputs.Auth;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.Application.UseCases.Auth;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class AuthCleanController
{
    private readonly LoginUseCase _loginUseCase;

    public AuthCleanController(LoginUseCase loginUseCase)
    {
        _loginUseCase = loginUseCase;
    }

    public LoginResponseDto? Login(LoginRequestDto loginRequestDto)
    {
        var output = _loginUseCase.Executar(new LoginInput(loginRequestDto.Username, loginRequestDto.Password));

        return output is null
            ? null
            : new LoginResponseDto
            {
                Token = output.Token,
                ExpiresAt = output.ExpiresAt
            };
    }
}
