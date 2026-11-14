using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarCancelamentoPagoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarCancelamentoPagoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ReparoPagoCancelado", "CancelamentoReparoPagoRecusado");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        MensagensCompensacao.ValidarVinculo(mensagem, saga, saga.PedidoCompensacaoId);
        var aceita = mensagem.Type == "ReparoPagoCancelado";
        if (aceita ? mensagem.Payload.Codigo is not null || mensagem.Payload.Motivo is not null
            : mensagem.Payload.Codigo != "ReparoNaoCancelavel" || string.IsNullOrWhiteSpace(mensagem.Payload.Motivo))
            throw new ValidacaoException("Resultado do cancelamento invalido.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        var alterada = saga.RegistrarCancelamentoPago(aceita, mensagem.OccurredAt, mensagem.Payload.Motivo);
        StatusOrdemServico? novoStatus = null;
        var registradoEm = mensagem.OccurredAt;
        if (alterada && aceita) novoStatus = StatusOrdemServico.CancelamentoPendente;
        else if (alterada && saga.Status == StatusSagaAbertura.ExecucaoLiberada)
        {
            // Uma liberacao observada durante a tentativa de cancelar nao havia avancado a OS.
            novoStatus = StatusOrdemServico.AguardandoExecucao;
            registradoEm = saga.ExecucaoLiberadaEm!.Value;
        }
        _sagas.Salvar(saga, mensagem.MessageId,
            aceita && alterada ? MensagensCompensacao.Estornar(saga, mensagem.MessageId) : null,
            novoStatus: novoStatus, registradoEm: registradoEm);
    }
}
