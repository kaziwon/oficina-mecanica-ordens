using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.UnitTests;

internal sealed class FakeCpfCnpjValidatorGateway : ICpfCnpjValidatorGateway
{
    public bool EhValidoResult { get; set; } = true;

    public bool EhValido(string? cpfCnpj)
    {
        return EhValidoResult && !string.IsNullOrWhiteSpace(cpfCnpj);
    }

    public string Normalizar(string cpfCnpj)
    {
        return new string(cpfCnpj.Where(char.IsDigit).ToArray());
    }
}

internal sealed class FakePlacaVeiculoValidatorGateway : IPlacaVeiculoValidatorGateway
{
    public bool EhValidaResult { get; set; } = true;

    public bool EhValida(string? placa)
    {
        return EhValidaResult && !string.IsNullOrWhiteSpace(placa);
    }

    public string Normalizar(string placa)
    {
        return placa.Trim().ToUpperInvariant().Replace("-", "");
    }
}
