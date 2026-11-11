using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarRespostaLiberacaoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarRespostaLiberacaoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "ExecucaoLiberada", "LiberacaoExecucaoRecusada");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload; var aceita = mensagem.Type == "ExecucaoLiberada";
        if (!saga.PedidoLiberacaoExecucaoId.HasValue || mensagem.CausationId != saga.PedidoLiberacaoExecucaoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId
            || !dados.OrcamentoId.HasValue || dados.OrcamentoId != saga.OrcamentoId
            || !dados.PagamentoId.HasValue || dados.PagamentoId != saga.PagamentoId
            || dados.ProvedorId != saga.PagamentoProvedorId || dados.ValorTotal != saga.ValorOrcamento || dados.Moeda != "BRL"
            || dados.Diagnostico is not null || dados.Itens is not null || dados.DescricaoSolicitacao is not null
            || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue
            || (aceita ? dados.Codigo is not null || dados.Motivo is not null
                : string.IsNullOrWhiteSpace(dados.Codigo) || string.IsNullOrWhiteSpace(dados.Motivo)))
            throw new ValidacaoException("A resposta nao corresponde ao pedido de liberacao.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        var alterada = saga.RegistrarRespostaLiberacao(aceita, mensagem.OccurredAt, dados.Motivo);
        _sagas.Salvar(saga, mensagem.MessageId, null,
            novoStatus: aceita && alterada ? StatusOrdemServico.AguardandoExecucao : null, registradoEm: mensagem.OccurredAt);
    }
}
