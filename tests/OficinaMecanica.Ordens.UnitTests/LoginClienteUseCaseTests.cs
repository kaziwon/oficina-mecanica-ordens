using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Inputs.Auth;
using OficinaMecanica.Ordens.Application.UseCases.Auth;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.UnitTests.Fakes;

namespace OficinaMecanica.Ordens.UnitTests;

public class LoginClienteUseCaseTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public void CpfValido_GeraTokenSomenteDoCadastroEncontrado(string cpf)
    {
        var clientes = new ClienteGatewayFake(); var token = new ClienteTokenGatewayFake();
        var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cliente", CpfCnpj = "52998224725" }; clientes.Clientes.Add(cliente);
        var resultado = new LoginClienteUseCase(clientes, new DocumentoLoginGatewayFake(), token).Executar(new(cpf));
        Assert.NotNull(resultado); Assert.Equal(cliente.Id, resultado.ClienteId); Assert.Equal(cliente.Id, token.ClienteId);
        Assert.Equal(cliente.Nome, token.Nome); Assert.Equal("token-cliente", resultado.Token); Assert.Equal(1, token.Chamadas);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000000000")]
    [InlineData("52998224724")]
    [InlineData("abc52998224725")]
    [InlineData("52998224725\n")]
    [InlineData("529 982 247 25")]
    [InlineData("529-982-247.25")]
    [InlineData("11222333000181")]
    [InlineData("５２９９８２２４７２５")]
    public void DocumentoInvalido_NaoEmiteToken(string? cpf)
    {
        var tokens = new ClienteTokenGatewayFake();
        var useCase = new LoginClienteUseCase(new ClienteGatewayFake(), new DocumentoLoginGatewayFake(), tokens);
        Assert.Throws<ValidacaoException>(() => useCase.Executar(new LoginClienteInput(cpf))); Assert.Equal(0, tokens.Chamadas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AusenteOuInativo_NaoEmiteToken(bool cadastrar)
    {
        var clientes = new ClienteGatewayFake(); var tokens = new ClienteTokenGatewayFake();
        if (cadastrar) clientes.Clientes.Add(new Cliente { Id = Guid.NewGuid(), CpfCnpj = "52998224725", Ativo = false });
        Assert.Null(new LoginClienteUseCase(clientes, new DocumentoLoginGatewayFake(), tokens).Executar(new("52998224725")));
        Assert.Equal(0, tokens.Chamadas);
    }
}
