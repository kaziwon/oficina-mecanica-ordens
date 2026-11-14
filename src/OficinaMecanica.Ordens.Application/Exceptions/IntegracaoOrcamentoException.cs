namespace OficinaMecanica.Ordens.Application.Exceptions;

public class IntegracaoOrcamentoException : Exception
{
    public IntegracaoOrcamentoException(string message) : base(message) { }
    public IntegracaoOrcamentoException(string message, Exception inner) : base(message, inner) { }
}
