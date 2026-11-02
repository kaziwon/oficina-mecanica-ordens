using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.UseCases.Clientes;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class ClienteUseCasesTests
{
    [Fact]
    public void Criar_DeveLancarExcecao_QuandoCpfCnpjJaExiste()
    {
        var gateway = new FakeClienteGateway
        {
            ExistePorCpfCnpjResult = true
        };
        var useCase = new CriarClienteUseCase(gateway, new FakeCpfCnpjValidatorGateway());
        var input = CriarInput();

        Assert.Throws<InvalidOperationException>(() => useCase.Executar(input));
    }

    [Fact]
    public void Criar_DeveRetornarClienteOutput_QuandoDadosForemValidos()
    {
        var gateway = new FakeClienteGateway();
        var useCase = new CriarClienteUseCase(gateway, new FakeCpfCnpjValidatorGateway());
        var input = CriarInput();

        var output = useCase.Executar(input);

        Assert.NotEqual(Guid.Empty, output.Id);
        Assert.Equal(input.Nome, output.Nome);
        Assert.Equal(input.CpfCnpj, output.CpfCnpj);
        Assert.Equal(input.Email, output.Email);
        Assert.Equal(input.Telefone, output.Telefone);
        Assert.Single(gateway.Clientes);
    }

    [Fact]
    public void Atualizar_DeveLancarExcecao_QuandoCpfCnpjJaExisteParaOutroCliente()
    {
        var gateway = new FakeClienteGateway
        {
            ExistePorCpfCnpjExcetoIdResult = true
        };
        var useCase = new AtualizarClienteUseCase(gateway, new FakeCpfCnpjValidatorGateway());

        Assert.Throws<InvalidOperationException>(() => useCase.Executar(CriarAtualizarInput(Guid.NewGuid())));
    }

    [Fact]
    public void Atualizar_DeveRetornarNull_QuandoClienteNaoExistir()
    {
        var gateway = new FakeClienteGateway();
        var useCase = new AtualizarClienteUseCase(gateway, new FakeCpfCnpjValidatorGateway());

        var output = useCase.Executar(CriarAtualizarInput(Guid.NewGuid()));

        Assert.Null(output);
    }

    [Fact]
    public void ObterTodos_DeveMapearClientesParaOutput()
    {
        var gateway = new FakeClienteGateway();
        gateway.Clientes.Add(new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = "Maria Oliveira",
            CpfCnpj = "11222333000181",
            Email = "maria@email.com",
            Telefone = "11999999999"
        });
        var useCase = new ObterTodosClientesUseCase(gateway);

        var output = useCase.Executar();

        Assert.Single(output);
        Assert.Equal("Maria Oliveira", output[0].Nome);
    }

    [Fact]
    public void Criar_DeveLancarValidacaoException_QuandoCpfCnpjForInvalido()
    {
        var gateway = new FakeClienteGateway();
        var cpfCnpjValidator = new FakeCpfCnpjValidatorGateway
        {
            EhValidoResult = false
        };
        var useCase = new CriarClienteUseCase(gateway, cpfCnpjValidator);

        Assert.Throws<ValidacaoException>(() => useCase.Executar(CriarInput()));
    }

    [Fact]
    public void Remover_DeveRetornarResultadoDoGateway()
    {
        var id = Guid.NewGuid();
        var gateway = new FakeClienteGateway();
        gateway.Clientes.Add(new Cliente
        {
            Id = id,
            Nome = "Maria Oliveira",
            CpfCnpj = "11222333000181",
            Email = "maria@email.com",
            Telefone = "11999999999"
        });
        var useCase = new RemoverClienteUseCase(gateway);

        var removido = useCase.Executar(id);

        Assert.True(removido);
        Assert.Empty(gateway.Clientes);
    }

    private static CriarClienteInput CriarInput()
    {
        return new CriarClienteInput(
            "Joao Silva",
            "52998224725",
            "joao@email.com",
            "11999999999");
    }

    private static AtualizarClienteInput CriarAtualizarInput(Guid id)
    {
        return new AtualizarClienteInput(
            id,
            "Joao Silva",
            "52998224725",
            "joao@email.com",
            "11999999999");
    }

    private sealed class FakeClienteGateway : IClienteGateway
    {
        public List<Cliente> Clientes { get; } = [];
        public bool ExistePorCpfCnpjResult { get; set; }
        public bool ExistePorCpfCnpjExcetoIdResult { get; set; }

        public Cliente Adicionar(Cliente cliente)
        {
            Clientes.Add(cliente);
            return cliente;
        }

        public Cliente? Atualizar(Cliente cliente)
        {
            var clienteExistente = Clientes.FirstOrDefault(c => c.Id == cliente.Id);

            if (clienteExistente is null)
            {
                return null;
            }

            clienteExistente.Nome = cliente.Nome;
            clienteExistente.CpfCnpj = cliente.CpfCnpj;
            clienteExistente.Email = cliente.Email;
            clienteExistente.Telefone = cliente.Telefone;

            return clienteExistente;
        }

        public bool ExistePorCpfCnpj(string cpfCnpj)
        {
            return ExistePorCpfCnpjResult || Clientes.Any(cliente => cliente.CpfCnpj == cpfCnpj);
        }

        public bool ExistePorCpfCnpjExcetoId(string cpfCnpj, Guid id)
        {
            return ExistePorCpfCnpjExcetoIdResult || Clientes.Any(cliente => cliente.CpfCnpj == cpfCnpj && cliente.Id != id);
        }

        public Cliente? ObterPorId(Guid id)
        {
            return Clientes.FirstOrDefault(cliente => cliente.Id == id);
        }

        public Cliente? ObterPorCpfCnpj(string cpfCnpj)
        {
            return Clientes.FirstOrDefault(cliente => cliente.CpfCnpj == cpfCnpj);
        }

        public List<Cliente> ObterTodos()
        {
            return Clientes;
        }

        public bool Remover(Guid id)
        {
            var cliente = Clientes.FirstOrDefault(c => c.Id == id);

            if (cliente is null)
            {
                return false;
            }

            Clientes.Remove(cliente);
            return true;
        }
    }
}
