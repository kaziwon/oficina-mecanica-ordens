using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.Outputs.Auth;

namespace OficinaMecanica.Ordens.Application.UseCases.Auth;

public class LoginClienteUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly ICpfCnpjValidatorGateway _documentos;
    private readonly IClienteTokenGateway _tokens;

    public LoginClienteUseCase(IClienteGateway clientes, ICpfCnpjValidatorGateway documentos, IClienteTokenGateway tokens)
    { _clientes = clientes; _documentos = documentos; _tokens = tokens; }

    public LoginClienteOutput? Executar(LoginClienteInput input)
    {
        var informado = input.Cpf ?? string.Empty;
        var formatado = informado.Length == 14 && informado[3] == '.' && informado[7] == '.' && informado[11] == '-';
        if ((!formatado && informado.Length != 11) || informado.Where((_, i) => !formatado || i is not (3 or 7 or 11))
                .Any(digito => digito is < '0' or > '9'))
            throw new ValidacaoException("Informe um CPF valido, com 11 digitos ou no formato 000.000.000-00.");
        var cpf = _documentos.Normalizar(informado);
        if (cpf.Length != 11 || !_documentos.EhValido(cpf))
            throw new ValidacaoException("O CPF informado e invalido.");
        var cliente = _clientes.ObterPorCpfCnpj(cpf);
        if (cliente is not { Ativo: true }) return null;
        var token = _tokens.GerarToken(cliente.Id, cliente.Nome);
        return new LoginClienteOutput { ClienteId = cliente.Id, Nome = cliente.Nome, Token = token.Token, ExpiresAt = token.ExpiresAt };
    }
}
