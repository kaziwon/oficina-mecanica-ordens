using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class DiagnosticoSagaTests
{
    [Fact]
    public void ConclusaoRepetida_NaoCriaOutroPedido()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), null);
        var evento = Guid.NewGuid(); var execucao = Guid.NewGuid(); var inicio = DateTime.UtcNow;
        Assert.True(saga.SolicitarOrcamento(evento, execucao, inicio, inicio));
        var pedido = saga.PedidoOrcamentoId;
        Assert.False(saga.SolicitarOrcamento(evento, execucao, inicio, inicio));
        Assert.Equal(pedido, saga.PedidoOrcamentoId);
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarOrcamento(Guid.NewGuid(), execucao, inicio, inicio));
    }

    [Fact]
    public void SagaCompensada_NaoReabreComDiagnosticoAtrasado()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), null);
        saga.SolicitarCancelamento("Desistencia"); saga.ConcluirCancelamento(true, null);
        Assert.False(saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow));
        Assert.Null(saga.PedidoOrcamentoId);
        Assert.Equal(StatusSagaAbertura.Compensada, saga.Status);
    }

    [Fact]
    public void DiagnosticoIniciado_BloqueiaCancelamentoDeEntrada()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), null);
        saga.RegistrarDiagnostico(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarCancelamento("Desistencia"));
    }
}
