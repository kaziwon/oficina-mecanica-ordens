namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IPlacaVeiculoValidatorSource
{
    bool EhValida(string? placa);
    string Normalizar(string placa);
}
