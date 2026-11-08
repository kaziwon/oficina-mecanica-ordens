using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Mappers.OrdensServico;

public static class SagaAberturaOutputMapper
{
    public static SagaAberturaOutput Mapear(SagaAbertura saga) => new()
    {
        Id = saga.Id,
        OrdemServicoId = saga.OrdemServicoId,
        ExecucaoId = saga.ExecucaoId,
        Status = saga.Status,
        CorrelationId = saga.CorrelationId,
        Motivo = saga.Motivo,
        CriadaEm = saga.CriadaEm,
        AtualizadaEm = saga.AtualizadaEm
    };
}
