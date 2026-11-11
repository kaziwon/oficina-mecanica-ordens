using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class LiberacaoSagaTests
{
    [Fact]
    public void SomentePagamentoConfirmado_PermiteSolicitarLiberacao()
    {
        var saga = Nova();
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarLiberacaoExecucao(Guid.NewGuid()));
        Assert.Null(saga.PedidoLiberacaoExecucaoId);
        saga.RegistrarResultadoPagamento(Guid.NewGuid(), "mock-id", true, DateTime.UtcNow, null);
        var evento = Guid.NewGuid(); saga.SolicitarLiberacaoExecucao(evento);
        Assert.Equal(StatusSagaAbertura.LiberacaoExecucaoPendente, saga.Status);
        Assert.Equal(evento, saga.EventoPagamentoConfirmadoId); Assert.NotNull(saga.PedidoLiberacaoExecucaoId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RespostaDaExecucao_PreservaPagamentoEImpedeCobrancaRepetida(bool aceita)
    {
        var saga = Nova(); var id = Guid.NewGuid();
        saga.RegistrarResultadoPagamento(id, "mock-id", true, DateTime.UtcNow, null);
        saga.SolicitarLiberacaoExecucao(Guid.NewGuid()); var pedido = saga.PedidoLiberacaoExecucaoId;
        saga.RegistrarRespostaLiberacao(aceita, DateTime.UtcNow, aceita ? null : "Execucao cancelada");
        Assert.Equal(aceita ? StatusSagaAbertura.ExecucaoLiberada : StatusSagaAbertura.FalhaRequerIntervencao, saga.Status);
        Assert.Equal(id, saga.PagamentoId); Assert.NotNull(saga.PagamentoConfirmadoEm);
        Assert.False(saga.RegistrarResultadoPagamento(id, "mock-id", true, DateTime.UtcNow, null));
        Assert.Equal(pedido, saga.PedidoLiberacaoExecucaoId);
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarLiberacaoExecucao(Guid.NewGuid()));
    }

    private static SagaAbertura Nova()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), "teste"); var agora = DateTime.UtcNow;
        saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), agora, agora); saga.ConfirmarOrcamento(Guid.NewGuid(), 200m);
        saga.ResponderOrcamento(Guid.NewGuid(), true, agora, agora, null); return saga;
    }
}
