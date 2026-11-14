using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.UnitTests.Fakes;

namespace OficinaMecanica.Ordens.UnitTests;

public class EntregaOrdemServicoTests
{
    [Fact]
    public void Entrega_RegistraDataUmaVezSemAlterarCadastro()
    {
        var ordem = Nova(); var antes = DateTime.UtcNow;
        Assert.True(ordem.Entregar());
        var registro = Assert.Single(ordem.Historico);
        Assert.Equal(StatusOrdemServico.Entregue, ordem.Status);
        Assert.Equal(ordem.Id, registro.OrdemServicoId); Assert.NotEqual(Guid.Empty, registro.Id);
        Assert.InRange(registro.RegistradoEm, antes, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, registro.RegistradoEm.Kind);
        Assert.False(ordem.Entregar()); Assert.Same(registro, Assert.Single(ordem.Historico));
        Assert.Equal("Revisao", ordem.DescricaoSolicitacao); Assert.NotEqual(Guid.Empty, ordem.ClienteId);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    [InlineData(StatusOrdemServico.AguardandoPagamento)]
    [InlineData(StatusOrdemServico.PagamentoConfirmado)]
    [InlineData(StatusOrdemServico.AguardandoExecucao)]
    [InlineData(StatusOrdemServico.CancelamentoPendente)]
    [InlineData((StatusOrdemServico)99)]
    public void SemConclusao_NaoEntrega(StatusOrdemServico status)
    {
        var ordem = Nova(); ordem.Status = status;
        Assert.Throws<InvalidOperationException>(() => ordem.Entregar());
        Assert.Equal(status, ordem.Status); Assert.Empty(ordem.Historico);
    }

    [Fact]
    public void UseCase_IdInexistenteRetornaNullSemGravar()
    {
        var gateway = new OrdemServicoGatewayFake();
        Assert.Null(new EntregarOrdemServicoUseCase(gateway).Executar(Guid.NewGuid()));
        Assert.Equal(0, gateway.TentativasEntrega);
    }

    [Fact]
    public void UseCase_EntregaERetornaMesmoHistoricoNasRepeticoes()
    {
        var gateway = new OrdemServicoGatewayFake(); var ordem = Nova(); gateway.Ordens.Add(ordem);
        var useCase = new EntregarOrdemServicoUseCase(gateway);
        var primeira = useCase.Executar(ordem.Id)!; var repetida = useCase.Executar(ordem.Id)!;
        Assert.Equal(StatusOrdemServico.Entregue, primeira.Status); Assert.Equal(primeira.Status, repetida.Status);
        Assert.Equal(Assert.Single(primeira.Historico).RegistradoEm, Assert.Single(repetida.Historico).RegistradoEm);
        Assert.Equal(1, gateway.TentativasEntrega); Assert.Single(gateway.Ordens[0].Historico);
    }

    [Fact]
    public void UseCase_ConflitoPersistenteNaoConfirmaEntregaEmMemoria()
    {
        var gateway = new OrdemServicoGatewayFake { RecusarEntrega = true }; var ordem = Nova(); gateway.Ordens.Add(ordem);
        Assert.Throws<InvalidOperationException>(() => new EntregarOrdemServicoUseCase(gateway).Executar(ordem.Id));
        Assert.Equal(5, gateway.TentativasEntrega); Assert.Equal(StatusOrdemServico.Finalizada, gateway.Ordens[0].Status);
        Assert.Empty(gateway.Ordens[0].Historico);
    }

    private static OrdemServico Nova() => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        VeiculoId = Guid.NewGuid(),
        Status = StatusOrdemServico.Finalizada,
        DescricaoSolicitacao = "Revisao",
        CriadaEm = DateTime.UtcNow
    };
}
