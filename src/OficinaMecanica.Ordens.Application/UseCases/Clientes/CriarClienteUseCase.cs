using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Application.Mappers.Clientes;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.Clientes;

public class CriarClienteUseCase
{
    private const string CpfCnpjInvalido = "O CPF/CNPJ informado é inválido.";
    private const string CpfCnpjJaCadastrado = "Ja existe um cliente cadastrado com este CPF/CNPJ.";
    private readonly IClienteGateway _clienteGateway;
    private readonly ICpfCnpjValidatorGateway _cpfCnpjValidatorGateway;

    public CriarClienteUseCase(IClienteGateway clienteGateway, ICpfCnpjValidatorGateway cpfCnpjValidatorGateway)
    {
        _clienteGateway = clienteGateway;
        _cpfCnpjValidatorGateway = cpfCnpjValidatorGateway;
    }

    public ClienteOutput Executar(CriarClienteInput input)
    {
        var cpfCnpj = ValidarCpfCnpj(input.CpfCnpj);

        if (_clienteGateway.ExistePorCpfCnpj(cpfCnpj))
        {
            throw new InvalidOperationException(CpfCnpjJaCadastrado);
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = input.Nome,
            CpfCnpj = cpfCnpj,
            Email = input.Email,
            Telefone = input.Telefone
        };

        var clienteAdicionado = _clienteGateway.Adicionar(cliente);

        return ClienteOutputMapper.Mapear(clienteAdicionado);
    }

    private string ValidarCpfCnpj(string cpfCnpj)
    {
        if (!_cpfCnpjValidatorGateway.EhValido(cpfCnpj))
        {
            throw new ValidacaoException(CpfCnpjInvalido);
        }

        return _cpfCnpjValidatorGateway.Normalizar(cpfCnpj);
    }
}
