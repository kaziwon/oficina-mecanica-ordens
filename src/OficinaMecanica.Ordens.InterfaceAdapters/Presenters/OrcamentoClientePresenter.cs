using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class OrcamentoClientePresenter
{
    public static OrcamentoClienteResponseDto Apresentar(OrcamentoClienteOutput output) => new()
    {
        Id = output.Id,
        OrdemServicoId = output.OrdemServicoId,
        ExecucaoId = output.ExecucaoId,
        Diagnostico = output.Diagnostico,
        Status = output.Status,
        ValorTotal = output.ValorTotal,
        CriadoEm = output.CriadoEm,
        RespondidoEm = output.RespondidoEm,
        MotivoRecusa = output.MotivoRecusa,
        Itens = output.Itens.Select(item => new ItemOrcamentoClienteResponseDto
        {
            Tipo = item.Tipo,
            ReferenciaId = item.ReferenciaId,
            Nome = item.Nome,
            Quantidade = item.Quantidade,
            PrecoUnitario = item.PrecoUnitario,
            Total = item.Total
        }).ToList()
    };
}
