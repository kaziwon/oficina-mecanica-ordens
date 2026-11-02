namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IPlacaVeiculoValidatorGateway
{
    bool EhValida(string? placa);
    string Normalizar(string placa);
}
