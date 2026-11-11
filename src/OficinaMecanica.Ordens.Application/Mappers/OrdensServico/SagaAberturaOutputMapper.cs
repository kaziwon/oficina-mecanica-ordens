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
        OrcamentoId = saga.OrcamentoId,
        ValorOrcamento = saga.ValorOrcamento,
        OrcamentoAprovado = saga.OrcamentoAprovado,
        OrcamentoRespondidoEm = saga.OrcamentoRespondidoEm,
        PagamentoId = saga.PagamentoId,
        PagamentoProvedorId = saga.PagamentoProvedorId,
        PagamentoConfirmadoEm = saga.PagamentoConfirmadoEm,
        ExecucaoLiberadaEm = saga.ExecucaoLiberadaEm,
        ReparoIniciadoEm = saga.ReparoIniciadoEm,
        ReparoConcluidoEm = saga.ReparoConcluidoEm,
        DiagnosticoIniciadoEm = saga.DiagnosticoIniciadoEm,
        DiagnosticoConcluidoEm = saga.DiagnosticoConcluidoEm,
        Status = saga.Status,
        CorrelationId = saga.CorrelationId,
        Motivo = saga.Motivo,
        CriadaEm = saga.CriadaEm,
        AtualizadaEm = saga.AtualizadaEm
    };
}
