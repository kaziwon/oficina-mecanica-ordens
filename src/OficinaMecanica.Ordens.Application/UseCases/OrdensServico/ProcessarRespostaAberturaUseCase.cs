using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarRespostaAberturaUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarRespostaAberturaUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ExecucaoRegistrada", "RegistroExecucaoRecusado",
            "EntradaExecucaoCancelada", "CancelamentoEntradaExecucaoRecusado");
        var saga = _sagas.ObterPorId(mensagem.SagaId)
            ?? throw new ValidacaoException("Saga desconhecida.");
        var registro = mensagem.Type is "ExecucaoRegistrada" or "RegistroExecucaoRecusado";
        var esperado = registro ? saga.PedidoRegistroId : saga.PedidoCancelamentoId;
        if (!esperado.HasValue || mensagem.CausationId != esperado
            || mensagem.Payload.OrdemServicoId != saga.OrdemServicoId || mensagem.CorrelationId != saga.CorrelationId
            || mensagem.Payload.DescricaoSolicitacao is not null)
            throw new ValidacaoException("Resposta nao corresponde ao comando, OS ou Saga esperados.");
        if (mensagem.Type == "ExecucaoRegistrada" && !mensagem.Payload.ExecucaoId.HasValue)
            throw new ValidacaoException("A confirmacao deve identificar a execucao.");
        var recusa = mensagem.Type.EndsWith("Recusado", StringComparison.Ordinal);
        if ((recusa && mensagem.Payload.ExecucaoId.HasValue)
            || (!recusa && (mensagem.Payload.Codigo is not null || mensagem.Payload.Motivo is not null))
            || (!registro && mensagem.Payload.ExecucaoId.HasValue && saga.ExecucaoId.HasValue
                && mensagem.Payload.ExecucaoId != saga.ExecucaoId))
            throw new ValidacaoException("Payload incompativel com o tipo de resposta ou a execucao conhecida.");
        if (recusa
            && (string.IsNullOrWhiteSpace(mensagem.Payload.Codigo) || string.IsNullOrWhiteSpace(mensagem.Payload.Motivo)))
            throw new ValidacaoException("A recusa precisa de codigo e motivo.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;

        switch (mensagem.Type)
        {
            case "ExecucaoRegistrada": saga.ConfirmarRegistro(mensagem.Payload.ExecucaoId!.Value); break;
            case "RegistroExecucaoRecusado":
                saga.RecusarRegistro(mensagem.Payload.Motivo!, mensagem.Payload.Codigo != "ProcessoCancelado");
                break;
            case "EntradaExecucaoCancelada": saga.ConcluirCancelamento(true, null); break;
            case "CancelamentoEntradaExecucaoRecusado": saga.ConcluirCancelamento(false, mensagem.Payload.Motivo); break;
        }
        _sagas.Salvar(saga, mensagem.MessageId, null,
            saga.Status is Domain.Entities.StatusSagaAbertura.Compensada or Domain.Entities.StatusSagaAbertura.RegistroRecusado);
    }
}
