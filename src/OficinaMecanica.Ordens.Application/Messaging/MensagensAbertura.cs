using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Messaging;

public static class MensagensAbertura
{
    public static MensagemIntegracao Registrar(SagaAbertura saga, string descricao) => new()
    {
        MessageId = saga.PedidoRegistroId,
        Type = "RegistrarExecucao",
        SagaId = saga.Id,
        OccurredAt = saga.CriadaEm,
        CorrelationId = saga.CorrelationId,
        Payload = new DadosMensagem { OrdemServicoId = saga.OrdemServicoId, DescricaoSolicitacao = descricao }
    };

    public static MensagemIntegracao Cancelar(SagaAbertura saga) => new()
    {
        MessageId = saga.PedidoCancelamentoId!.Value,
        Type = "CancelarEntradaExecucao",
        SagaId = saga.Id,
        CausationId = saga.PedidoRegistroId,
        OccurredAt = saga.AtualizadaEm,
        CorrelationId = saga.CorrelationId,
        Payload = new DadosMensagem { OrdemServicoId = saga.OrdemServicoId, Motivo = saga.Motivo }
    };
}
