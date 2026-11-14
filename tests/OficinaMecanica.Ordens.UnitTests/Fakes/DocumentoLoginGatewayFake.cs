using OficinaMecanica.Ordens.Application.Gateways;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public class DocumentoLoginGatewayFake : ICpfCnpjValidatorGateway
{
    public bool EhValido(string? documento) => documento is "52998224725" or "11144477735";
    public string Normalizar(string documento) => new(documento.Where(char.IsAsciiDigit).ToArray());
}
