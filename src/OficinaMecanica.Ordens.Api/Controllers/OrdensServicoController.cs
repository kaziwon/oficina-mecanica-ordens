using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.Ordens.Controllers.Mappers;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using HttpDtos = OficinaMecanica.Ordens.Controllers.DTOs;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class OrdensServicoController : ControllerBase
{
    private readonly OrdensServicoCleanController _controller;

    public OrdensServicoController(OrdensServicoCleanController controller)
    {
        _controller = controller;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrdemServicoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Post([FromBody] HttpDtos.OrdemServicoRequestDto request)
    {
        var ordem = _controller.Criar(request.ParaCleanDto(), HttpContext.TraceIdentifier);
        return CreatedAtAction(nameof(GetById), new { id = ordem.Id }, ordem);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrdemServicoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var ordem = _controller.ObterPorId(id);
        return ordem is null ? NotFound() : Ok(ordem);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OrdemServicoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Get([FromQuery] StatusOrdemServico? status, [FromQuery] string? ordenacao)
        => Ok(_controller.ObterTodas(status, ordenacao));
}
