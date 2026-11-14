using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OficinaMecanica.Ordens.Authentication;
using OficinaMecanica.Ordens.Controllers.Mappers;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using HttpDto = OficinaMecanica.Ordens.Controllers.DTOs.LoginClienteRequestDto;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Route("api/auth/cliente")]
[AllowAnonymous]
[EnableRateLimiting(ClienteAuthentication.LoginRateLimit)]
public class AuthClienteController(AuthClienteCleanController controller) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LoginClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Login([FromBody] HttpDto request)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var output = controller.Login(request.ParaCleanDto());
        return output is null ? Unauthorized() : Ok(output);
    }
}
