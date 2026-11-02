using System.ComponentModel.DataAnnotations;

namespace OficinaMecanica.Ordens.Controllers.DTOs;

public class ClienteRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    public string CpfCnpj { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;
}
