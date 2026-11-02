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
public class ClientesController : ControllerBase
{
    private readonly ClientesCleanController _clientesCleanController;

    public ClientesController(ClientesCleanController clientesCleanController)
    {
        _clientesCleanController = clientesCleanController;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ClienteResponseDto>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(_clientesCleanController.ObterTodos());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var cliente = _clientesCleanController.ObterPorId(id);

        if (cliente is null)
        {
            return NotFound();
        }

        return Ok(cliente);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Post([FromBody] HttpDtos.ClienteRequestDto clienteRequestDto)
    {
        try
        {
            var clienteAdicionado = _clientesCleanController.Criar(clienteRequestDto.ParaCleanDto());

            return CreatedAtAction(nameof(GetById), new { id = clienteAdicionado.Id }, clienteAdicionado);
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
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Put(Guid id, [FromBody] HttpDtos.ClienteRequestDto clienteRequestDto)
    {
        try
        {
            var clienteAtualizado = _clientesCleanController.Atualizar(id, clienteRequestDto.ParaCleanDto());

            if (clienteAtualizado is null)
            {
                return NotFound();
            }

            return Ok(clienteAtualizado);
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
        var removido = _clientesCleanController.Remover(id);

        if (!removido)
        {
            return NotFound();
        }

        return NoContent();
    }
}
