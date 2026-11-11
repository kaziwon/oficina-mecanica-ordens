using OficinaMecanica.Ordens.Application.Exceptions;

namespace OficinaMecanica.Ordens.Application.Messaging;

public static class MensagemValidator
{
    public static void Validar(MensagemIntegracao mensagem, params string[] tipos)
    {
        if (mensagem is null || mensagem.Version != 1 || !tipos.Contains(mensagem.Type)
            || mensagem.MessageId == Guid.Empty || mensagem.SagaId == Guid.Empty
            || mensagem.Payload is null || mensagem.Payload.OrdemServicoId == Guid.Empty
            || mensagem.OccurredAt == default || mensagem.OccurredAt.Kind != DateTimeKind.Utc
            || string.IsNullOrWhiteSpace(mensagem.CorrelationId) || mensagem.CorrelationId.Length > 100
            || mensagem.CorrelationId.Any(char.IsControl))
            throw new ValidacaoException("Envelope de integracao invalido ou versao desconhecida.");
        if (mensagem.CausationId == Guid.Empty || mensagem.Payload.ExecucaoId == Guid.Empty
            || mensagem.Payload.DescricaoSolicitacao?.Length > 1000
            || mensagem.Payload.Motivo?.Length > 1000 || mensagem.Payload.Codigo?.Length > 100)
            throw new ValidacaoException("Dados da mensagem de integracao invalidos.");
        if (mensagem.Payload.Diagnostico?.Length > 2000 || mensagem.Payload.OrcamentoId == Guid.Empty
            || (mensagem.Payload.Itens is not null && mensagem.Payload.Itens.Any(item => item is null)))
            throw new ValidacaoException("Snapshot de diagnostico ou orcamento invalido.");
        if (mensagem.Payload.PagamentoId == Guid.Empty || mensagem.Payload.ProvedorId?.Length > 100
            || mensagem.Payload.Moeda?.Length > 3)
            throw new ValidacaoException("Identificacao de pagamento invalida.");
        if ((mensagem.Payload.PagamentoId.HasValue || mensagem.Payload.ProvedorId is not null || mensagem.Payload.Moeda is not null)
            && mensagem.Type is not ("SolicitarPagamento" or "PagamentoConfirmado" or "PagamentoRecusado" or "FalhaProcessamentoPagamento"))
            throw new ValidacaoException("O tipo de mensagem nao admite dados de pagamento.");
        if (mensagem.Payload.OrcamentoCriadoEm.HasValue
            && mensagem.Type is not ("OrcamentoAprovado" or "OrcamentoRecusado"))
            throw new ValidacaoException("O tipo de mensagem nao admite a data de criacao do orcamento.");
        if (mensagem.Type is "RegistrarExecucao" or "CancelarEntradaExecucao" or "ExecucaoRegistrada"
            or "RegistroExecucaoRecusado" or "EntradaExecucaoCancelada" or "CancelamentoEntradaExecucaoRecusado")
        {
            var dados = mensagem.Payload;
            if (dados.Diagnostico is not null || dados.Itens is not null || dados.OrcamentoId.HasValue
                || dados.ValorTotal.HasValue || dados.DiagnosticoIniciadoEm.HasValue || dados.DiagnosticoConcluidoEm.HasValue)
                throw new ValidacaoException("Mensagem de abertura nao aceita dados de diagnostico ou orcamento.");
        }
    }
}
