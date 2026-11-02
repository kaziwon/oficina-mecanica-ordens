namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface ICpfCnpjValidatorSource
{
    bool EhValido(string? cpfCnpj);
    string Normalizar(string cpfCnpj);
}
