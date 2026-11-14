using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarResultadoEstornoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarResultadoEstornoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "EstornoConfirmado", "EstornoRecusado", "FalhaProcessamentoEstorno");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        MensagensCompensacao.ValidarVinculo(mensagem, saga, saga.PedidoEstornoId);
        var confirmado = mensagem.Type == "EstornoConfirmado";
        var falhaTecnica = mensagem.Type == "FalhaProcessamentoEstorno";
        if (mensagem.Payload.EstornoId != saga.PedidoEstornoId
            || (falhaTecnica ? mensagem.Payload.EstornoProvedorId is not null
                : string.IsNullOrWhiteSpace(mensagem.Payload.EstornoProvedorId) || mensagem.Payload.EstornoProvedorId.Any(char.IsControl))
            || (confirmado ? string.IsNullOrWhiteSpace(mensagem.Payload.EstornoProvedorId)
                || mensagem.Payload.Codigo is not null || mensagem.Payload.Motivo is not null
                : string.IsNullOrWhiteSpace(mensagem.Payload.Motivo)
                    || mensagem.Payload.Codigo != (mensagem.Type == "EstornoRecusado" ? "EstornoRecusado" : "ResultadoEstornoNaoConfirmado")))
            throw new ValidacaoException("Resultado do estorno invalido.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        var alterada = saga.RegistrarEstorno(confirmado, mensagem.Payload.EstornoProvedorId, mensagem.OccurredAt, mensagem.Payload.Motivo);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: confirmado && alterada ? StatusOrdemServico.Cancelada : null, registradoEm: mensagem.OccurredAt);
    }
}
