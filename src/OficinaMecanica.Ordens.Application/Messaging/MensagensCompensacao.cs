using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Messaging;

public static class MensagensCompensacao
{
    public static MensagemIntegracao Cancelar(SagaAbertura saga) => Criar(saga,
        "CancelarReparoPago", saga.PedidoCompensacaoId!.Value, saga.PedidoLiberacaoExecucaoId!.Value, saga.MotivoCompensacao);

    public static MensagemIntegracao Estornar(SagaAbertura saga, Guid eventoCancelamento)
    {
        var mensagem = Criar(saga, "SolicitarEstorno", saga.PedidoEstornoId!.Value, eventoCancelamento, null);
        mensagem.Payload.EstornoId = mensagem.MessageId;
        return mensagem;
    }

    public static void ValidarVinculo(MensagemIntegracao mensagem, SagaAbertura saga, Guid? pedidoId)
    {
        var dados = mensagem.Payload;
        if (!pedidoId.HasValue || mensagem.CausationId != pedidoId || mensagem.CorrelationId != saga.CorrelationId
            || dados.OrdemServicoId != saga.OrdemServicoId || dados.ExecucaoId != saga.ExecucaoId
            || dados.OrcamentoId != saga.OrcamentoId || dados.PagamentoId != saga.PagamentoId
            || dados.ProvedorId != saga.PagamentoProvedorId || dados.ValorTotal != saga.ValorOrcamento)
            throw new ValidacaoException("Resposta de compensacao diverge do pedido persistido.");
    }

    private static MensagemIntegracao Criar(SagaAbertura saga, string tipo, Guid id, Guid causa, string? motivo) => new()
    {
        MessageId = id,
        Type = tipo,
        SagaId = saga.Id,
        CausationId = causa,
        CorrelationId = saga.CorrelationId,
        OccurredAt = DateTime.UtcNow,
        Payload = new DadosMensagem
        {
            OrdemServicoId = saga.OrdemServicoId,
            ExecucaoId = saga.ExecucaoId,
            OrcamentoId = saga.OrcamentoId,
            PagamentoId = saga.PagamentoId,
            ProvedorId = saga.PagamentoProvedorId,
            ValorTotal = saga.ValorOrcamento,
            Moeda = "BRL",
            Motivo = motivo
        }
    };
}
