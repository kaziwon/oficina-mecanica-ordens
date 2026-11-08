using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests;

public class SagaAberturaTests
{
    [Fact]
    public void Registro_ECompensacao_RespeitamOrdemENaoReabremProcesso()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), null);
        Assert.Equal(StatusSagaAbertura.AguardandoRegistro, saga.Status);
        Assert.False(string.IsNullOrWhiteSpace(saga.CorrelationId));
        saga.ConfirmarRegistro(Guid.NewGuid());
        Assert.Equal(StatusSagaAbertura.RegistroConfirmado, saga.Status);
        Assert.True(saga.SolicitarCancelamento("Cliente desistiu"));
        var pedido = saga.PedidoCancelamentoId;
        Assert.False(saga.SolicitarCancelamento("Repeticao"));
        Assert.Equal(pedido, saga.PedidoCancelamentoId);
        saga.ConcluirCancelamento(true, null);
        saga.ConfirmarRegistro(Guid.NewGuid());
        Assert.Equal(StatusSagaAbertura.Compensada, saga.Status);
        Assert.False(saga.SolicitarCancelamento("Repeticao"));
    }

    [Fact]
    public void CancelamentoAntesDaConfirmacao_NaoEhRevertidoPorRespostaAtrasada()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), "correlacao");
        saga.SolicitarCancelamento("Cliente desistiu");
        saga.ConfirmarRegistro(Guid.NewGuid());
        saga.RecusarRegistro("Resposta atrasada");
        Assert.Equal(StatusSagaAbertura.CompensacaoPendente, saga.Status);
        saga.ConcluirCancelamento(false, "Diagnostico ja iniciado");
        Assert.Equal(StatusSagaAbertura.FalhaRequerIntervencao, saga.Status);
        Assert.Throws<InvalidOperationException>(() => saga.SolicitarCancelamento("Outra tentativa"));
    }

    [Fact]
    public void RecusaDefinitiva_NaoPermiteConfirmacaoPosterior()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), "correlacao");
        saga.RecusarRegistro("Conflito de vinculo");
        saga.ConfirmarRegistro(Guid.NewGuid());
        Assert.Equal(StatusSagaAbertura.RegistroRecusado, saga.Status);
        Assert.Equal("Conflito de vinculo", saga.Motivo);
    }

    [Fact]
    public void ConfirmacaoDeOutraExecucao_EhRejeitada()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), "correlacao");
        saga.ConfirmarRegistro(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => saga.ConfirmarRegistro(Guid.NewGuid()));
    }

    [Fact]
    public void Comandos_PreservamIdentidadeECausalidade()
    {
        var saga = SagaAbertura.Iniciar(Guid.NewGuid(), "correlacao");
        var registrar = MensagensAbertura.Registrar(saga, "Revisao");
        MensagemValidator.Validar(registrar, "RegistrarExecucao");
        Assert.Equal(saga.PedidoRegistroId, registrar.MessageId);
        saga.SolicitarCancelamento("Cliente desistiu");
        var cancelar = MensagensAbertura.Cancelar(saga);
        Assert.Equal(registrar.MessageId, cancelar.CausationId);
        Assert.Equal(registrar.SagaId, cancelar.SagaId);
        Assert.Equal(registrar.CorrelationId, cancelar.CorrelationId);
    }
}
