namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class ClienteRequestDto
{
    public string Nome { get; set; } = string.Empty;
    public string CpfCnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
}
