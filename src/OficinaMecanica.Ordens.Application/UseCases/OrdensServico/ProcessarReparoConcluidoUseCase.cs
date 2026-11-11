using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarReparoConcluidoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarReparoConcluidoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ReparoConcluido");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        if (!saga.PedidoLiberacaoExecucaoId.HasValue || mensagem.CausationId != saga.PedidoLiberacaoExecucaoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId
            || !dados.OrcamentoId.HasValue || dados.OrcamentoId != saga.OrcamentoId
            || !dados.PagamentoId.HasValue || dados.PagamentoId != saga.PagamentoId
            || dados.ProvedorId != saga.PagamentoProvedorId || dados.ValorTotal != saga.ValorOrcamento || dados.Moeda != "BRL"
            || !dados.ReparoIniciadoEm.HasValue || dados.ReparoConcluidoEm != mensagem.OccurredAt
            || dados.ReparoIniciadoEm > mensagem.OccurredAt
            || (saga.ReparoIniciadoEm.HasValue && dados.ReparoIniciadoEm != saga.ReparoIniciadoEm)
            || dados.Diagnostico is not null || dados.Itens is not null || dados.DescricaoSolicitacao is not null
            || dados.Codigo is not null || dados.Motivo is not null
            || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue)
            throw new ValidacaoException("A conclusao do reparo diverge da autorizacao e do inicio da Saga.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        if (saga.Status is StatusSagaAbertura.LiberacaoExecucaoPendente or StatusSagaAbertura.ExecucaoLiberada)
            throw new InvalidOperationException("Aguarde a confirmacao do inicio antes de reprocessar a conclusao.");
        var alterada = saga.RegistrarConclusaoReparo(dados.ReparoIniciadoEm.Value, mensagem.OccurredAt);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: alterada ? StatusOrdemServico.Finalizada : null, registradoEm: mensagem.OccurredAt);
    }
}
