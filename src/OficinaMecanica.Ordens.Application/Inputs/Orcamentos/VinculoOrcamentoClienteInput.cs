namespace OficinaMecanica.Ordens.Application.Inputs.Orcamentos;

public record VinculoOrcamentoClienteInput(Guid OrcamentoId, Guid OrdemServicoId, Guid ExecucaoId, Guid SagaId,
    Guid PedidoGeracaoId, decimal ValorTotal, string CorrelationId);
