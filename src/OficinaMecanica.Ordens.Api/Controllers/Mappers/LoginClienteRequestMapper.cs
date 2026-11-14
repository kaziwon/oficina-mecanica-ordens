using HttpDto = OficinaMecanica.Ordens.Controllers.DTOs.LoginClienteRequestDto;
using CleanDto = OficinaMecanica.Ordens.InterfaceAdapters.DTOs.LoginClienteRequestDto;

namespace OficinaMecanica.Ordens.Controllers.Mappers;

public static class LoginClienteRequestMapper
{
    public static CleanDto ParaCleanDto(this HttpDto request) => new() { Cpf = request.Cpf };
}
