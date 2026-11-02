using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Controllers.Mappers;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using HttpDtos = OficinaMecanica.Ordens.Controllers.DTOs;

namespace OficinaMecanica.Ordens.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class VeiculosController : ControllerBase
{
    private readonly VeiculosCleanController _veiculosCleanController;

    public VeiculosController(VeiculosCleanController veiculosCleanController)
    {
        _veiculosCleanController = veiculosCleanController;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<VeiculoResponseDto>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(_veiculosCleanController.ObterTodos());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VeiculoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var veiculo = _veiculosCleanController.ObterPorId(id);

        return veiculo is null ? NotFound() : Ok(veiculo);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VeiculoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Post([FromBody] HttpDtos.VeiculoRequestDto veiculoRequestDto)
    {
        try
        {
            var veiculo = _veiculosCleanController.Criar(veiculoRequestDto.ParaCleanDto());
            return CreatedAtAction(nameof(GetById), new { id = veiculo.Id }, veiculo);
        }
        catch (ValidacaoException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VeiculoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Put(Guid id, [FromBody] HttpDtos.VeiculoRequestDto veiculoRequestDto)
    {
        try
        {
            var veiculo = _veiculosCleanController.Atualizar(id, veiculoRequestDto.ParaCleanDto());

            return veiculo is null ? NotFound() : Ok(veiculo);
        }
        catch (ValidacaoException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        var removido = _veiculosCleanController.Remover(id);

        return removido ? NoContent() : NotFound();
    }
}
