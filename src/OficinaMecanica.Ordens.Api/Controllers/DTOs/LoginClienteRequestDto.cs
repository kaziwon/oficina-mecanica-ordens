using System.ComponentModel.DataAnnotations;

namespace OficinaMecanica.Ordens.Controllers.DTOs;

public class LoginClienteRequestDto
{
    [Required]
    [StringLength(14)]
    public string Cpf { get; set; } = string.Empty;
}
