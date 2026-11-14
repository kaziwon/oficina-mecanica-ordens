using OficinaMecanica.Ordens.Application.Outputs.Auth;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class LoginClientePresenter
{
    public static LoginClienteResponseDto Apresentar(LoginClienteOutput output) => new()
    { ClienteId = output.ClienteId, Nome = output.Nome, Token = output.Token, ExpiresAt = output.ExpiresAt };
}
