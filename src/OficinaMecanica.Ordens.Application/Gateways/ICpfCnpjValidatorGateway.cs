namespace OficinaMecanica.Ordens.Application.Gateways;

public interface ICpfCnpjValidatorGateway
{
    bool EhValido(string? cpfCnpj);
    string Normalizar(string cpfCnpj);
}
