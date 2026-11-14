using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.Ordens.Controllers.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/ordensservico/{ordemServicoId:guid}")]
public class SagaAberturaController : ControllerBase
{
    private readonly SagaAberturaCleanController _controller;
    public SagaAberturaController(SagaAberturaCleanController controller) { _controller = controller; }

    [HttpGet("saga-abertura")]
    [ProducesResponseType(typeof(SagaAberturaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(Guid ordemServicoId)
    {
        var saga = _controller.Obter(ordemServicoId);
        return saga is null ? NotFound() : Ok(saga);
    }

    [HttpPost("cancelar-reparo")]
    [ProducesResponseType(typeof(SagaAberturaResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult CancelarPago(Guid ordemServicoId, [FromBody] CancelarEntradaRequestDto dto)
    {
        var saga = _controller.CancelarPago(ordemServicoId, dto.Motivo);
        return saga is null ? NotFound() : AcceptedAtAction(nameof(Get), new { ordemServicoId }, saga);
    }

    [HttpPost("cancelar-entrada")]
    [ProducesResponseType(typeof(SagaAberturaResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Cancelar(Guid ordemServicoId, [FromBody] CancelarEntradaRequestDto dto)
    {
        var saga = _controller.Cancelar(ordemServicoId, dto.Motivo);
        return saga is null ? NotFound() : AcceptedAtAction(nameof(Get), new { ordemServicoId }, saga);
    }
}
