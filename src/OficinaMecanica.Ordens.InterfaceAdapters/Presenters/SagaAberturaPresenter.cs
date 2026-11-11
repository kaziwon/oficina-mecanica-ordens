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
        OrcamentoId = saga.OrcamentoId,
        ValorOrcamento = saga.ValorOrcamento,
        OrcamentoAprovado = saga.OrcamentoAprovado,
        OrcamentoRespondidoEm = saga.OrcamentoRespondidoEm,
        PagamentoId = saga.PagamentoId,
        PagamentoProvedorId = saga.PagamentoProvedorId,
        PagamentoConfirmadoEm = saga.PagamentoConfirmadoEm,
        ExecucaoLiberadaEm = saga.ExecucaoLiberadaEm,
        DiagnosticoIniciadoEm = saga.DiagnosticoIniciadoEm,
        DiagnosticoConcluidoEm = saga.DiagnosticoConcluidoEm,
        Status = saga.Status,
        CorrelationId = saga.CorrelationId,
        Motivo = saga.Motivo,
        CriadaEm = saga.CriadaEm,
        AtualizadaEm = saga.AtualizadaEm
    };
}
