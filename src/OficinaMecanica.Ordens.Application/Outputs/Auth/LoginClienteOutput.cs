namespace OficinaMecanica.Ordens.Application.Outputs.Auth;

public class LoginClienteOutput
{
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
