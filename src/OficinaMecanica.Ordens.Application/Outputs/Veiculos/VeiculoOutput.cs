using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
namespace OficinaMecanica.Ordens.Application.Outputs.Veiculos;

public class VeiculoOutput
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Ano { get; set; }
}
