using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarReparoIniciadoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarReparoIniciadoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ReparoIniciado");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        if (!saga.PedidoLiberacaoExecucaoId.HasValue || mensagem.CausationId != saga.PedidoLiberacaoExecucaoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId
            || !dados.OrcamentoId.HasValue || dados.OrcamentoId != saga.OrcamentoId
            || !dados.PagamentoId.HasValue || dados.PagamentoId != saga.PagamentoId
            || dados.ProvedorId != saga.PagamentoProvedorId || dados.ValorTotal != saga.ValorOrcamento || dados.Moeda != "BRL"
            || dados.ReparoIniciadoEm != mensagem.OccurredAt || dados.Diagnostico is not null
            || dados.Itens is not null || dados.DescricaoSolicitacao is not null || dados.Codigo is not null || dados.Motivo is not null
            || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue)
            throw new ValidacaoException("O inicio do reparo diverge da autorizacao da Saga.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        // Filas padrao podem entregar o inicio antes da confirmacao da liberacao.
        if (saga.Status == StatusSagaAbertura.LiberacaoExecucaoPendente)
            throw new InvalidOperationException("Aguarde a confirmacao da liberacao antes de reprocessar o inicio.");
        var alterada = saga.RegistrarInicioReparo(mensagem.OccurredAt);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: alterada ? StatusOrdemServico.EmExecucao : null, registradoEm: mensagem.OccurredAt);
    }
}
