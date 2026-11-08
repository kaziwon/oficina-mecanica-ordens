using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarDecisaoOrcamentoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarDecisaoOrcamentoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "OrcamentoAprovado", "OrcamentoRecusado");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        var aprovado = mensagem.Type == "OrcamentoAprovado";
        if (!saga.PedidoOrcamentoId.HasValue || mensagem.CausationId != saga.PedidoOrcamentoId
            || mensagem.CorrelationId != saga.CorrelationId || dados.OrdemServicoId != saga.OrdemServicoId
            || !dados.ExecucaoId.HasValue || dados.ExecucaoId != saga.ExecucaoId || !dados.OrcamentoId.HasValue
            || !dados.ValorTotal.HasValue || dados.ValorTotal <= 0 || dados.ValorTotal > 999999999.99m
            || decimal.Round(dados.ValorTotal.Value, 2) != dados.ValorTotal
            || dados.OrcamentoCriadoEm is not { Kind: DateTimeKind.Utc } criadoEm || criadoEm == default
            || criadoEm > mensagem.OccurredAt || dados.Diagnostico is not null || dados.Itens is not null
            || dados.DescricaoSolicitacao is not null || dados.DiagnosticoIniciadoEm.HasValue
            || dados.DiagnosticoConcluidoEm.HasValue || dados.Codigo is not null
            || (aprovado ? dados.Motivo is not null : string.IsNullOrWhiteSpace(dados.Motivo)))
            throw new ValidacaoException("Resposta do cliente nao corresponde ao pedido, vinculos ou contrato.");
        if (saga.OrcamentoId.HasValue && (saga.OrcamentoId != dados.OrcamentoId || saga.ValorOrcamento != dados.ValorTotal))
            throw new ValidacaoException("Resposta diverge do orcamento conhecido.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;
        // A decisao tambem comprova a criacao: as filas nao garantem que OrcamentoGerado chegue primeiro.
        if (saga.Status == StatusSagaAbertura.OrcamentoPendente)
            saga.ConfirmarOrcamento(dados.OrcamentoId.Value, dados.ValorTotal.Value);
        var alterada = saga.ResponderOrcamento(mensagem.MessageId, aprovado, criadoEm, mensagem.OccurredAt, dados.Motivo);
        MensagemIntegracao? comando = null;
        if (alterada && !aprovado)
            comando = new MensagemIntegracao
            {
                MessageId = saga.PedidoCancelamentoPorRecusaId!.Value,
                Type = "CancelarExecucaoPorRecusaOrcamento",
                SagaId = saga.Id,
                CausationId = mensagem.MessageId,
                CorrelationId = saga.CorrelationId,
                OccurredAt = DateTime.UtcNow,
                Payload = new DadosMensagem
                {
                    OrdemServicoId = saga.OrdemServicoId,
                    ExecucaoId = saga.ExecucaoId,
                    OrcamentoId = saga.OrcamentoId,
                    Motivo = saga.Motivo
                }
            };
        _sagas.Salvar(saga, mensagem.MessageId, comando,
            novoStatus: aprovado ? StatusOrdemServico.AguardandoPagamento : StatusOrdemServico.AguardandoAprovacao,
            registradoEm: criadoEm);
    }
}
