using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarCancelamentoPorRecusaUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarCancelamentoPorRecusaUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ExecucaoCanceladaPorRecusaOrcamento", "CancelamentoPorRecusaOrcamentoRecusado");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        var sucesso = mensagem.Type == "ExecucaoCanceladaPorRecusaOrcamento";
        if (!saga.PedidoCancelamentoPorRecusaId.HasValue || mensagem.CausationId != saga.PedidoCancelamentoPorRecusaId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId
            || !dados.OrcamentoId.HasValue || dados.OrcamentoId != saga.OrcamentoId
            || dados.ValorTotal.HasValue || dados.Diagnostico is not null || dados.Itens is not null
            || dados.DescricaoSolicitacao is not null || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue
            || (sucesso ? dados.Codigo is not null || dados.Motivo is not null
                : string.IsNullOrWhiteSpace(dados.Codigo) || string.IsNullOrWhiteSpace(dados.Motivo)))
            throw new ValidacaoException("Confirmacao de cancelamento nao corresponde ao pedido esperado.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        if (saga.Status != StatusSagaAbertura.CancelamentoPorRecusaPendente)
            throw new ValidacaoException("A Saga nao aguarda esta resposta de cancelamento.");
        saga.ConcluirCancelamentoPorRecusa(sucesso, dados.Motivo);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: sucesso ? StatusOrdemServico.Cancelada : null, registradoEm: mensagem.OccurredAt);
    }
}
