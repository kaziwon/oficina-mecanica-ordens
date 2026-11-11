using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class InicioReparoTests
{
    [Fact]
    public void Saga_SoIniciaDepoisDaLiberacaoENaoRegrideComRespostaTardia()
    {
        var agora = DateTime.UtcNow;
        var saga = new SagaAbertura { Status = StatusSagaAbertura.LiberacaoExecucaoPendente, PagamentoConfirmadoEm = agora };
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarInicioReparo(agora));
        saga.RegistrarRespostaLiberacao(true, agora, null);
        Assert.True(saga.RegistrarInicioReparo(agora.AddSeconds(1)));
        Assert.False(saga.RegistrarInicioReparo(agora.AddSeconds(1)));
        Assert.False(saga.RegistrarRespostaLiberacao(true, agora, null));
        Assert.Equal(StatusSagaAbertura.EmExecucao, saga.Status);
        Assert.Throws<InvalidOperationException>(() => saga.RegistrarInicioReparo(agora.AddSeconds(2)));
    }
}
