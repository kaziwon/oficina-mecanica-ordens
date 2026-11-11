using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class ConclusaoReparoTests
{
    [Fact]
    public void Saga_SoConcluiDepoisDoInicioENaoRegrideComEventosTardios()
    {
        var inicio = DateTime.UtcNow; var fim = inicio.AddMinutes(1);
        var saga = new SagaAbertura { Status = StatusSagaAbertura.ExecucaoLiberada, ExecucaoLiberadaEm = inicio, PagamentoConfirmadoEm = inicio };
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarConclusaoReparo(inicio, fim));
        saga.RegistrarInicioReparo(inicio);
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarConclusaoReparo(inicio, inicio.AddSeconds(-1)));
        Assert.True(saga.RegistrarConclusaoReparo(inicio, fim)); Assert.False(saga.RegistrarConclusaoReparo(inicio, fim));
        Assert.False(saga.RegistrarInicioReparo(inicio)); Assert.False(saga.RegistrarRespostaLiberacao(true, inicio, null));
        Assert.Equal(StatusSagaAbertura.Finalizada, saga.Status); Assert.Equal(fim, saga.ReparoConcluidoEm);
    }

    [Fact]
    public void OS_FinalizaMasNaoMarcaEntrega()
    {
        var ordem = new OrdemServico { Id = Guid.NewGuid(), Status = StatusOrdemServico.AguardandoExecucao };
        Assert.Throws<InvalidOperationException>(() => ordem.ConcluirReparo(DateTime.UtcNow));
        ordem.IniciarReparo(DateTime.UtcNow); ordem.ConcluirReparo(DateTime.UtcNow); ordem.ConcluirReparo(DateTime.UtcNow);
        Assert.Equal(StatusOrdemServico.Finalizada, ordem.Status); Assert.Equal(2, ordem.Historico.Count);
    }
}
