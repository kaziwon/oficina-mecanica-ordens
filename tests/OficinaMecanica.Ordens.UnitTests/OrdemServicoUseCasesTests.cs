using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Inputs.OrdensServico;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.UnitTests.Fakes;

namespace OficinaMecanica.Ordens.UnitTests;

public class OrdemServicoUseCasesTests
{
    private readonly CadastroGatewayFake _cadastros = new();
    private readonly OrdemServicoGatewayFake _ordens = new();
    private readonly FakeCpfCnpjValidatorGateway _documentos = new();
    private readonly FakePlacaVeiculoValidatorGateway _placas = new();

    private CriarOrdemServicoUseCase CriarUseCase() => new(_ordens, _cadastros, _cadastros, _documentos, _placas);

    [Fact]
    public void Criar_ComCadastroCompleto_PreparaUmUnicoGrafoSemPersistirCadastrosSeparadamente()
    {
        var output = CriarUseCase().Executar(NovoCadastro());
        var ordem = Assert.Single(_ordens.Ordens);

        Assert.Empty(_cadastros.Clientes);
        Assert.Empty(_cadastros.Veiculos);
        Assert.NotNull(ordem.Cliente);
        Assert.NotNull(ordem.Veiculo);
        Assert.Equal(ordem.Cliente.Id, ordem.Veiculo.ClienteId);
        Assert.Equal(ordem.ClienteId, ordem.Cliente.Id);
        Assert.Equal(ordem.VeiculoId, ordem.Veiculo.Id);
        Assert.Equal(StatusOrdemServico.Recebida, output.Status);
        Assert.Equal(output.CriadaEm, Assert.Single(output.Historico).RegistradoEm);
    }

    [Fact]
    public void Criar_ComCadastroExistente_VinculaIdsSemRecriarClienteEVeiculo()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), CpfCnpj = "52998224725" };
        var veiculo = new Veiculo { Id = Guid.NewGuid(), ClienteId = cliente.Id };
        _cadastros.Clientes.Add(cliente);
        _cadastros.Veiculos.Add(veiculo);

        var output = CriarUseCase().Executar(new(cliente.CpfCnpj, veiculo.Id, null, null, "Trocar oleo"));

        Assert.Equal(cliente.Id, output.ClienteId);
        Assert.Equal(veiculo.Id, output.VeiculoId);
        Assert.Null(Assert.Single(_ordens.Ordens).Cliente);
        Assert.Null(_ordens.Ordens[0].Veiculo);
    }

    [Fact]
    public void Criar_RejeitaVeiculoDeOutroCliente()
    {
        _cadastros.Clientes.Add(new Cliente { Id = Guid.NewGuid(), CpfCnpj = "52998224725" });
        var veiculo = new Veiculo { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid() };
        _cadastros.Veiculos.Add(veiculo);

        Assert.Throws<InvalidOperationException>(() => CriarUseCase().Executar(
            new("52998224725", veiculo.Id, null, null, "Revisao")));
        Assert.Empty(_ordens.Ordens);
    }

    [Fact]
    public void Criar_RejeitaClienteInexistente()
    {
        Assert.Throws<InvalidOperationException>(() => CriarUseCase().Executar(
            new("52998224725", Guid.NewGuid(), null, null, "Revisao")));
    }

    [Fact]
    public void Criar_RejeitaVeiculoInexistente()
    {
        _cadastros.Clientes.Add(new Cliente { Id = Guid.NewGuid(), CpfCnpj = "52998224725" });
        Assert.Throws<InvalidOperationException>(() => CriarUseCase().Executar(
            new("52998224725", Guid.NewGuid(), null, null, "Revisao")));
    }

    [Fact]
    public void Criar_RejeitaOpcoesMisturadas()
    {
        var input = NovoCadastro();
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(
            new("52998224725", Guid.NewGuid(), input.Cliente, input.Veiculo, "Revisao")));
    }

    [Fact]
    public void Criar_RejeitaOpcaoIncompleta()
    {
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(
            new(null, null, null, null, "Revisao")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_RejeitaDescricaoVazia(string descricao)
    {
        var input = NovoCadastro();
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(
            new(null, null, input.Cliente, input.Veiculo, descricao)));
    }

    [Fact]
    public void Criar_RejeitaDescricaoLonga()
    {
        var input = NovoCadastro();
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(
            new(null, null, input.Cliente, input.Veiculo, new string('x', 1001))));
    }

    [Fact]
    public void Criar_RejeitaDocumentoInvalidoSemPersistir()
    {
        _documentos.EhValidoResult = false;
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(NovoCadastro()));
        Assert.Empty(_ordens.Ordens);
    }

    [Fact]
    public void Criar_RejeitaPlacaInvalidaSemPersistir()
    {
        _placas.EhValidaResult = false;
        Assert.Throws<ValidacaoException>(() => CriarUseCase().Executar(NovoCadastro()));
        Assert.Empty(_ordens.Ordens);
    }

    [Fact]
    public void Criar_RejeitaDocumentoDuplicado()
    {
        _cadastros.Clientes.Add(new Cliente { CpfCnpj = "52998224725" });
        Assert.Throws<InvalidOperationException>(() => CriarUseCase().Executar(NovoCadastro()));
    }

    [Fact]
    public void Criar_RejeitaPlacaDuplicada()
    {
        _cadastros.Veiculos.Add(new Veiculo { Placa = "ABC1D23" });
        Assert.Throws<InvalidOperationException>(() => CriarUseCase().Executar(NovoCadastro()));
    }

    [Fact]
    public void Obter_RetornaNullQuandoNaoExiste()
    {
        Assert.Null(new ObterOrdemServicoPorIdUseCase(_ordens).Executar(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("maisAntigo")]
    [InlineData("maisNovo")]
    public void Listar_AceitaOrdenacoesConhecidas(string? ordenacao)
    {
        CriarUseCase().Executar(NovoCadastro());
        Assert.Single(new ObterTodasOrdensServicoUseCase(_ordens).Executar(null, ordenacao));
    }

    [Fact]
    public void Listar_RejeitaOrdenacaoInvalida()
    {
        Assert.Throws<ValidacaoException>(() => new ObterTodasOrdensServicoUseCase(_ordens).Executar(null, "invalido"));
    }

    [Fact]
    public void Listar_RejeitaStatusInvalido()
    {
        Assert.Throws<ValidacaoException>(() => new ObterTodasOrdensServicoUseCase(_ordens)
            .Executar((StatusOrdemServico)99, null));
    }

    private static CriarOrdemServicoInput NovoCadastro() => new(null, null,
        new CriarClienteInput("Cliente Teste", "52998224725", "teste@example.com", "11999999999"),
        new CriarVeiculoDaOrdemInput("ABC1D23", "Fiat", "Uno", 2020), "Revisao");
}
