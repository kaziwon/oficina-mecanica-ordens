using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ProcessarEventoDiagnosticoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public ProcessarEventoDiagnosticoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }

    public void Executar(MensagemIntegracao mensagem)
    {
        MensagemValidator.Validar(mensagem, "DiagnosticoIniciado", "DiagnosticoConcluido");
        var saga = _sagas.ObterPorId(mensagem.SagaId) ?? throw new ValidacaoException("Saga desconhecida.");
        var dados = mensagem.Payload;
        var concluido = mensagem.Type == "DiagnosticoConcluido";
        if (mensagem.CausationId != saga.PedidoRegistroId || mensagem.CorrelationId != saga.CorrelationId
            || dados.OrdemServicoId != saga.OrdemServicoId || !dados.ExecucaoId.HasValue
            || (saga.ExecucaoId.HasValue && saga.ExecucaoId != dados.ExecucaoId)
            || dados.DiagnosticoIniciadoEm is not { Kind: DateTimeKind.Utc } inicio || inicio == default
            || dados.DescricaoSolicitacao is not null || dados.Codigo is not null || dados.Motivo is not null
            || dados.OrcamentoId.HasValue || dados.ValorTotal.HasValue)
            throw new ValidacaoException("Evento de diagnostico nao corresponde a Saga ou ao contrato.");
        if (concluido ? dados.DiagnosticoConcluidoEm is not { Kind: DateTimeKind.Utc } fim
                || fim < inicio || dados.Itens is null
                || string.IsNullOrWhiteSpace(dados.Diagnostico)
            : dados.DiagnosticoConcluidoEm.HasValue || dados.Itens is not null || dados.Diagnostico is not null)
            throw new ValidacaoException("Snapshot ou datas do diagnostico invalidos.");
        if (_sagas.FoiProcessada(mensagem.MessageId)) return;

        MensagemIntegracao? comando = null;
        var aceito = concluido
            ? saga.SolicitarOrcamento(mensagem.MessageId, dados.ExecucaoId.Value, inicio, dados.DiagnosticoConcluidoEm!.Value)
            : saga.RegistrarDiagnostico(dados.ExecucaoId.Value, inicio);
        if (aceito && concluido)
            comando = new MensagemIntegracao
            {
                MessageId = saga.PedidoOrcamentoId!.Value,
                Type = "GerarOrcamento",
                SagaId = saga.Id,
                CausationId = mensagem.MessageId,
                CorrelationId = saga.CorrelationId,
                OccurredAt = DateTime.UtcNow,
                Payload = new DadosMensagem
                {
                    OrdemServicoId = saga.OrdemServicoId,
                    ExecucaoId = saga.ExecucaoId,
                    Diagnostico = dados.Diagnostico,
                    Itens = dados.Itens
                }
            };
        _sagas.Salvar(saga, mensagem.MessageId, comando,
            novoStatus: saga.DiagnosticoIniciadoEm.HasValue ? StatusOrdemServico.EmDiagnostico : null);
    }
}
