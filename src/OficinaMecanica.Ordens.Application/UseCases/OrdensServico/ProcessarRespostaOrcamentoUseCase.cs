using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarRespostaOrcamentoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarRespostaOrcamentoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "OrcamentoGerado", "GeracaoOrcamentoRecusada");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        var sucesso = mensagem.Type == "OrcamentoGerado";
        if (!saga.PedidoOrcamentoId.HasValue || mensagem.CausationId != saga.PedidoOrcamentoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || dados.ExecucaoId != saga.ExecucaoId || dados.Diagnostico is not null || dados.Itens is not null
            || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue
            || dados.DescricaoSolicitacao is not null)
            throw new ValidacaoException("Resposta nao corresponde ao pedido de orcamento.");
        if (sucesso ? !dados.OrcamentoId.HasValue || !dados.ValorTotal.HasValue
                || dados.ValorTotal <= 0 || dados.ValorTotal > 999999999.99m
                || decimal.Round(dados.ValorTotal.Value, 2) != dados.ValorTotal
                || dados.Codigo is not null || dados.Motivo is not null
            : dados.OrcamentoId.HasValue || dados.ValorTotal.HasValue
                || string.IsNullOrWhiteSpace(dados.Codigo) || string.IsNullOrWhiteSpace(dados.Motivo))
            throw new ValidacaoException("Payload da resposta de orcamento invalido.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        if (saga.OrcamentoId.HasValue && (!sucesso || saga.OrcamentoId != dados.OrcamentoId || saga.ValorOrcamento != dados.ValorTotal))
            throw new ValidacaoException("Resposta contradiz o orcamento ja confirmado.");
        if (sucesso) saga.ConfirmarOrcamento(dados.OrcamentoId!.Value, dados.ValorTotal!.Value);
        else saga.RecusarOrcamento(dados.Motivo!);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: saga.Status == StatusSagaAbertura.OrcamentoGerado ? StatusOrdemServico.AguardandoAprovacao : null,
            registradoEm: mensagem.OccurredAt);
    }
}
