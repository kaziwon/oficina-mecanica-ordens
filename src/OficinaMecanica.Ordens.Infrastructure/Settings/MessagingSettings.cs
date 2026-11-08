namespace OficinaMecanica.Ordens.Infrastructure.Settings;

public class MessagingSettings
{
    public bool Enabled { get; set; }
    public bool UsarAws { get; set; }
    public string Endpoint { get; set; } = "http://localhost:9324";
    public string Region { get; set; } = "us-east-1";
    public string ComandosQueue { get; set; } = "execucao-comandos";
    public string EventosQueue { get; set; } = "ordens-eventos";
    public string OrcamentosQueue { get; set; } = "cobranca-comandos";
}
