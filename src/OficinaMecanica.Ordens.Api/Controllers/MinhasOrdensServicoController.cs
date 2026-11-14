using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.Ordens.Authentication;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Route("api/minhas-ordens")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(AuthenticationSchemes = ClienteAuthentication.Scheme, Roles = "Cliente")]
public class MinhasOrdensServicoController(MinhasOrdensServicoCleanController controller) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<OrdemServicoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult Get()
    {
        var clienteId = ObterClienteId();
        return clienteId.HasValue ? Ok(controller.ObterTodas(clienteId.Value)) : Unauthorized();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrdemServicoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var clienteId = ObterClienteId();
        if (!clienteId.HasValue) return Unauthorized();
        var ordem = controller.ObterPorId(clienteId.Value, id);
        return ordem is null ? NotFound() : Ok(ordem);
    }

    private Guid? ObterClienteId()
    {
        var identidades = User.FindAll("sub").ToList();
        return identidades.Count == 1 && Guid.TryParse(identidades[0].Value, out var id) && id != Guid.Empty ? id : null;
    }
}
