using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OficinaMecanica.Ordens.Authentication;
using OficinaMecanica.Ordens.Controllers.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Route("api/minhas-ordens/{id:guid}/orcamento")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(AuthenticationSchemes = ClienteAuthentication.Scheme, Roles = "Cliente")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
public class MeuOrcamentoController(MeuOrcamentoCleanController controller) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OrcamentoClienteResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellation)
    {
        var clienteId = ObterClienteId();
        if (!clienteId.HasValue) return Unauthorized();
        var response = await controller.Obter(clienteId.Value, id, cancellation);
        return response is null ? NotFound() : Ok(response);
    }
    [HttpPost("aprovar")]
    [ProducesResponseType(typeof(OrcamentoClienteResponseDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Aprovar(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AprovarMeuOrcamentoRequestDto? request,
        CancellationToken cancellation) => Responder(id, true, null, cancellation);
    [HttpPost("recusar")]
    [ProducesResponseType(typeof(OrcamentoClienteResponseDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Recusar(Guid id, [FromBody] RecusarMeuOrcamentoRequestDto request, CancellationToken cancellation)
        => Responder(id, false, request.Motivo, cancellation);
    private async Task<IActionResult> Responder(Guid id, bool aprovado, string? motivo, CancellationToken cancellation)
    {
        var clienteId = ObterClienteId();
        if (!clienteId.HasValue) return Unauthorized();
        var response = await controller.Responder(clienteId.Value, id, aprovado, motivo, cancellation);
        return response is null ? NotFound() : Ok(response);
    }
    private Guid? ObterClienteId()
    {
        var identidades = User.FindAll("sub").ToList();
        return identidades.Count == 1 && Guid.TryParse(identidades[0].Value, out var id) && id != Guid.Empty ? id : null;
    }
}
