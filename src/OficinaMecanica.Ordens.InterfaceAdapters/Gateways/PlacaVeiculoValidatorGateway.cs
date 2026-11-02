using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class PlacaVeiculoValidatorGateway : IPlacaVeiculoValidatorGateway
{
    private readonly IPlacaVeiculoValidatorSource _placaVeiculoValidatorSource;

    public PlacaVeiculoValidatorGateway(IPlacaVeiculoValidatorSource placaVeiculoValidatorSource)
    {
        _placaVeiculoValidatorSource = placaVeiculoValidatorSource;
    }

    public bool EhValida(string? placa)
    {
        return _placaVeiculoValidatorSource.EhValida(placa);
    }

    public string Normalizar(string placa)
    {
        return _placaVeiculoValidatorSource.Normalizar(placa);
    }
}
