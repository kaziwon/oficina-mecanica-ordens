using BrazilianDocuments.Validators;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class BrazilianDocumentsPlacaVeiculoValidatorSource : IPlacaVeiculoValidatorSource
{
    public bool EhValida(string? placa)
    {
        if (string.IsNullOrWhiteSpace(placa))
        {
            return false;
        }

        return PlacaVeicularValidator.IsValid(Normalizar(placa));
    }

    public string Normalizar(string placa)
    {
        return PlacaVeicularValidator.ClearCode(placa).ToUpperInvariant();
    }
}
