using OficinaMecanica.Ordens.Domain.Entities;
namespace OficinaMecanica.Ordens.UnitTests;

public class CompensacaoPagaTests
{
    [Fact]
    public void Compensacao_ExigeParadaAntesDoEstornoEGuardaMesmosIds()
    {
        var saga = Paga(); Assert.True(saga.SolicitarCompensacaoPaga("Desistencia")); var pedido = saga.PedidoCompensacaoId;
        Assert.False(saga.SolicitarCompensacaoPaga("Desistencia")); Assert.Equal(pedido, saga.PedidoCompensacaoId);
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarEstorno(true, "refund", DateTime.UtcNow, null));
        var cancelada = DateTime.UtcNow; Assert.True(saga.RegistrarCancelamentoPago(true, cancelada, null));
        var estorno = saga.PedidoEstornoId; Assert.NotNull(estorno);
        Assert.False(saga.RegistrarCancelamentoPago(true, cancelada, null)); Assert.Equal(estorno, saga.PedidoEstornoId);
        Assert.True(saga.RegistrarEstorno(true, "refund", DateTime.UtcNow, null));
        Assert.False(saga.RegistrarEstorno(true, "refund", DateTime.UtcNow, null));
        Assert.Equal(StatusSagaAbertura.CompensadaAposPagamento, saga.Status); Assert.NotNull(saga.PagamentoConfirmadoEm);
        Assert.False(saga.RegistrarEstorno(false, null, DateTime.UtcNow, "Falha tecnica atrasada"));
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarEstorno(false, "refund-outro", DateTime.UtcNow, "contradicao"));
    }
    [Theory]
    [InlineData(StatusSagaAbertura.Finalizada)]
    [InlineData(StatusSagaAbertura.EmDiagnostico)]
    [InlineData(StatusSagaAbertura.OrcamentoGerado)]
    [InlineData(StatusSagaAbertura.AguardandoPagamento)]
    public void EstadoNaoCompensavel_NaoSolicita(StatusSagaAbertura status)
    {
        var saga = Paga(); saga.Status = status;
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarCompensacaoPaga("Cancelar")); Assert.Null(saga.PedidoCompensacaoId);
    }
    [Fact]
    public void SemPagamentoConfirmado_NaoSolicitaEstorno()
    {
        var saga = Paga(); saga.PagamentoConfirmadoEm = null;
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarCompensacaoPaga("Cancelar"));
    }
    [Fact]
    public void CancelamentoRecusado_RestauraFluxoSemEstorno()
    {
        var saga = Paga(); saga.SolicitarCompensacaoPaga("Cancelar");
        saga.RegistrarCancelamentoPago(false, DateTime.UtcNow, "Reparo concluido");
        Assert.Equal(StatusSagaAbertura.EmExecucao, saga.Status); Assert.Null(saga.PedidoEstornoId); Assert.NotNull(saga.CompensacaoRecusadaEm);
        saga.RegistrarConclusaoReparo(saga.ReparoIniciadoEm!.Value, DateTime.UtcNow);
        Assert.Equal(StatusSagaAbertura.Finalizada, saga.Status);
    }
    [Fact]
    public void LiberacaoObservadaDuranteCancelamento_NaoSePerdeNaRecusa()
    {
        var saga = Paga(); saga.Status = StatusSagaAbertura.LiberacaoExecucaoPendente; saga.ExecucaoLiberadaEm = null; saga.ReparoIniciadoEm = null;
        saga.SolicitarCompensacaoPaga("Cancelar"); Assert.False(saga.RegistrarRespostaLiberacao(true, DateTime.UtcNow, null));
        saga.RegistrarCancelamentoPago(false, DateTime.UtcNow, "Recusa");
        Assert.Equal(StatusSagaAbertura.ExecucaoLiberada, saga.Status); Assert.Null(saga.PedidoEstornoId);
    }
    [Fact]
    public void InicioAtrasado_NaoReabreExecucaoCancelada()
    {
        var saga = Paga(); saga.Status = StatusSagaAbertura.ExecucaoLiberada; saga.ReparoIniciadoEm = null;
        saga.SolicitarCompensacaoPaga("Cancelar"); var inicio = DateTime.UtcNow;
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarInicioReparo(inicio));
        saga.RegistrarCancelamentoPago(true, inicio.AddSeconds(1), null);
        Assert.False(saga.RegistrarInicioReparo(inicio)); Assert.Equal(StatusSagaAbertura.EstornoPendente, saga.Status);
        saga.RegistrarEstorno(false, null, DateTime.UtcNow, "Resultado incerto");
        Assert.Equal(StatusSagaAbertura.EstornoRequerIntervencao, saga.Status); Assert.Null(saga.EstornadoEm);
    }
    private static SagaAbertura Paga() => new()
    {
        Status = StatusSagaAbertura.EmExecucao,
        PagamentoId = Guid.NewGuid(),
        ExecucaoId = Guid.NewGuid(),
        OrcamentoId = Guid.NewGuid(),
        PedidoLiberacaoExecucaoId = Guid.NewGuid(),
        PagamentoConfirmadoEm = DateTime.UtcNow.AddSeconds(-3),
        ExecucaoLiberadaEm = DateTime.UtcNow.AddSeconds(-2),
        ReparoIniciadoEm = DateTime.UtcNow.AddSeconds(-1)
    };
}
