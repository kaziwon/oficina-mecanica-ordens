using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class SagaAberturaPresenter
{
    public static SagaAberturaResponseDto Apresentar(SagaAberturaOutput saga) => new()
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
