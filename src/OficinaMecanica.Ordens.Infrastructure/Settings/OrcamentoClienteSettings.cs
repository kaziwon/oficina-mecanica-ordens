namespace OficinaMecanica.Ordens.Infrastructure.Settings;

public class OrcamentoClienteSettings
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
