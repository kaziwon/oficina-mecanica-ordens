using System.Text.RegularExpressions;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class CpfCnpjValidatorSource : ICpfCnpjValidatorSource
{
    public bool EhValido(string? cpfCnpj)
    {
        if (string.IsNullOrWhiteSpace(cpfCnpj))
        {
            return false;
        }

        var apenasNumeros = Normalizar(cpfCnpj);

        return apenasNumeros.Length switch
        {
            11 => EhCpfValido(apenasNumeros),
            14 => EhCnpjValido(apenasNumeros),
            _ => false
        };
    }

    public string Normalizar(string cpfCnpj)
    {
        return Regex.Replace(cpfCnpj, "[^0-9]", "");
    }

    private static bool EhCpfValido(string cpf)
    {
        if (TodosOsDigitosSaoIguais(cpf))
        {
            return false;
        }

        var somaPrimeiroDigito = 0;

        for (var i = 0; i < 9; i++)
        {
            somaPrimeiroDigito += (cpf[i] - '0') * (10 - i);
        }

        var restoPrimeiroDigito = somaPrimeiroDigito % 11;
        var primeiroDigitoVerificador = restoPrimeiroDigito < 2 ? 0 : 11 - restoPrimeiroDigito;

        if (cpf[9] - '0' != primeiroDigitoVerificador)
        {
            return false;
        }

        var somaSegundoDigito = 0;

        for (var i = 0; i < 10; i++)
        {
            somaSegundoDigito += (cpf[i] - '0') * (11 - i);
        }

        var restoSegundoDigito = somaSegundoDigito % 11;
        var segundoDigitoVerificador = restoSegundoDigito < 2 ? 0 : 11 - restoSegundoDigito;

        return cpf[10] - '0' == segundoDigitoVerificador;
    }

    private static bool EhCnpjValido(string cnpj)
    {
        if (TodosOsDigitosSaoIguais(cnpj))
        {
            return false;
        }

        int[] pesosPrimeiroDigito = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesosSegundoDigito = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var somaPrimeiroDigito = 0;

        for (var i = 0; i < 12; i++)
        {
            somaPrimeiroDigito += (cnpj[i] - '0') * pesosPrimeiroDigito[i];
        }

        var restoPrimeiroDigito = somaPrimeiroDigito % 11;
        var primeiroDigitoVerificador = restoPrimeiroDigito < 2 ? 0 : 11 - restoPrimeiroDigito;

        if (cnpj[12] - '0' != primeiroDigitoVerificador)
        {
            return false;
        }

        var somaSegundoDigito = 0;

        for (var i = 0; i < 13; i++)
        {
            somaSegundoDigito += (cnpj[i] - '0') * pesosSegundoDigito[i];
        }

        var restoSegundoDigito = somaSegundoDigito % 11;
        var segundoDigitoVerificador = restoSegundoDigito < 2 ? 0 : 11 - restoSegundoDigito;

        return cnpj[13] - '0' == segundoDigitoVerificador;
    }

    private static bool TodosOsDigitosSaoIguais(string valor)
    {
        return valor.All(digito => digito == valor[0]);
    }
}
