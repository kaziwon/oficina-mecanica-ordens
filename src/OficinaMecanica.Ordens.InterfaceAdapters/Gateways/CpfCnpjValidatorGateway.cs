using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class CpfCnpjValidatorGateway : ICpfCnpjValidatorGateway
{
    private readonly ICpfCnpjValidatorSource _cpfCnpjValidatorSource;

    public CpfCnpjValidatorGateway(ICpfCnpjValidatorSource cpfCnpjValidatorSource)
    {
        _cpfCnpjValidatorSource = cpfCnpjValidatorSource;
    }

    public bool EhValido(string? cpfCnpj)
    {
        return _cpfCnpjValidatorSource.EhValido(cpfCnpj);
    }

    public string Normalizar(string cpfCnpj)
    {
        return _cpfCnpjValidatorSource.Normalizar(cpfCnpj);
    }
}
