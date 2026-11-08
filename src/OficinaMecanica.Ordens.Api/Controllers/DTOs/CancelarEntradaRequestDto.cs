using System.ComponentModel.DataAnnotations;

namespace OficinaMecanica.Ordens.Controllers.DTOs;

public class CancelarEntradaRequestDto
{
    [Required, StringLength(1000)]
    public string Motivo { get; set; } = string.Empty;
}
