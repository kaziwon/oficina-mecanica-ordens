namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class OrdemServicoVeiculoRequestDto
{
    public string Placa { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Ano { get; set; }
}
