namespace OficinaMecanica.Ordens.Infrastructure.Settings;

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ClienteAudience { get; set; } = "OficinaMecanica.Ordens.Cliente";
    public string SecretKey { get; set; } = string.Empty;
}
