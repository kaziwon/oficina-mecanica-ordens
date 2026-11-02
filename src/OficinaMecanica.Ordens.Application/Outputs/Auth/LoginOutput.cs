using OficinaMecanica.Ordens.Application.Inputs.Auth;
namespace OficinaMecanica.Ordens.Application.Outputs.Auth;

public class LoginOutput
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
