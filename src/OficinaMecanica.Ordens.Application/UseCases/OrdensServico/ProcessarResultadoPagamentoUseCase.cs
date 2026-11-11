using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarResultadoPagamentoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarResultadoPagamentoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "PagamentoConfirmado", "PagamentoRecusado", "FalhaProcessamentoPagamento");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload; var confirmado = mensagem.Type == "PagamentoConfirmado";
        var falhaTecnica = mensagem.Type == "FalhaProcessamentoPagamento";
        if (!saga.PedidoPagamentoId.HasValue || mensagem.CausationId != saga.PedidoPagamentoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId
            || !dados.OrcamentoId.HasValue || dados.OrcamentoId != saga.OrcamentoId
            || !dados.PagamentoId.HasValue || dados.ValorTotal != saga.ValorOrcamento || dados.Moeda != "BRL"
            || dados.Diagnostico is not null || dados.Itens is not null || dados.DescricaoSolicitacao is not null
            || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue
            || (falhaTecnica ? dados.ProvedorId is not null : string.IsNullOrWhiteSpace(dados.ProvedorId) || dados.ProvedorId.Any(char.IsControl))
            || (confirmado ? dados.Codigo is not null || dados.Motivo is not null
                : string.IsNullOrWhiteSpace(dados.Motivo) || dados.Codigo != (falhaTecnica ? "ResultadoNaoConfirmado" : "PagamentoRecusado")))
            throw new ValidacaoException("Resultado nao corresponde ao pagamento esperado.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        var alterada = saga.RegistrarResultadoPagamento(dados.PagamentoId.Value, dados.ProvedorId, confirmado, mensagem.OccurredAt, dados.Motivo);
        MensagemIntegracao? comando = null;
        if (confirmado && alterada)
        {
            saga.SolicitarLiberacaoExecucao(mensagem.MessageId);
            comando = new MensagemIntegracao
            {
                MessageId = saga.PedidoLiberacaoExecucaoId!.Value,
                Type = "LiberarExecucao",
                SagaId = saga.Id,
                CausationId = mensagem.MessageId,
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
                    Moeda = "BRL"
                }
            };
        }
        _sagas.Salvar(saga, mensagem.MessageId, comando,
            novoStatus: confirmado && alterada ? StatusOrdemServico.PagamentoConfirmado : null, registradoEm: mensagem.OccurredAt);
    }
}
