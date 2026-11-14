namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class LoginClienteResponseDto
{
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
