using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class RespostaOrcamentoSagaTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resposta_DefineProximaEtapaSemLiberarReparo(bool aprovado)
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), null); var agora = DateTime.UtcNow;
        saga.SolicitarOrcamento(Guid.NewGuid(), Guid.NewGuid(), agora, agora);
        saga.ConfirmarOrcamento(Guid.NewGuid(), 100m);
        var evento = Guid.NewGuid();
        Assert.True(saga.ResponderOrcamento(evento, aprovado, agora, agora, aprovado ? null : "Valor alto"));
        Assert.False(saga.ResponderOrcamento(evento, aprovado, agora, agora, aprovado ? null : "Valor alto"));
        Assert.Equal(aprovado ? StatusSagaAbertura.AguardandoPagamento : StatusSagaAbertura.CancelamentoPorRecusaPendente, saga.Status);
        if (!aprovado) Assert.NotNull(saga.PedidoCancelamentoPorRecusaId);
        Assert.Throws<InvalidOperationException>(() => saga.ResponderOrcamento(Guid.NewGuid(), !aprovado, agora, agora, "Outra resposta"));
    }
}
