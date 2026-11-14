using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

namespace OficinaMecanica.Ordens.Application.Validators;

public static class OrcamentoClienteValidator
{
    public static void Validar(OrcamentoClienteOutput orcamento, VinculoOrcamentoClienteInput vinculo)
    {
        if (orcamento is null || orcamento.Id != vinculo.OrcamentoId || orcamento.OrdemServicoId != vinculo.OrdemServicoId
            || orcamento.ExecucaoId != vinculo.ExecucaoId || orcamento.ValorTotal != vinculo.ValorTotal
            || orcamento.Status is not ("AguardandoAprovacao" or "Aprovado" or "Recusado")
            || string.IsNullOrWhiteSpace(orcamento.Diagnostico) || orcamento.Diagnostico.Length > 2000
            || orcamento.CriadoEm == default || orcamento.CriadoEm.Kind != DateTimeKind.Utc
            || orcamento.Itens is null || orcamento.Itens.Count is < 1 or > 100)
            throw new IntegracaoOrcamentoException("Resposta de Cobranca nao corresponde ao orcamento da OS.");
        if (orcamento.Itens.Any(item => item is null || item.Tipo is not ("Servico" or "PecaInsumo") || item.ReferenciaId == Guid.Empty
            || string.IsNullOrWhiteSpace(item.Nome) || item.Nome.Length > 150 || item.Quantidade is < 1 or > 10000
            || item.PrecoUnitario <= 0 || item.PrecoUnitario > 999999.99m || decimal.Round(item.PrecoUnitario, 2) != item.PrecoUnitario
            || item.Total != item.Quantidade * item.PrecoUnitario)
            || orcamento.Itens.Sum(item => item.Total) != orcamento.ValorTotal
            || orcamento.Itens.Select(item => (item.Tipo, item.ReferenciaId)).Distinct().Count() != orcamento.Itens.Count)
            throw new IntegracaoOrcamentoException("Itens de Cobranca nao correspondem ao total do orcamento.");
        if (orcamento.Status == "AguardandoAprovacao" ? orcamento.RespondidoEm.HasValue || orcamento.MotivoRecusa is not null
            : orcamento.RespondidoEm is not { Kind: DateTimeKind.Utc } respondido || respondido < orcamento.CriadoEm
                || (orcamento.Status == "Aprovado" ? orcamento.MotivoRecusa is not null : string.IsNullOrWhiteSpace(orcamento.MotivoRecusa)))
            throw new IntegracaoOrcamentoException("Estado da resposta do orcamento inconsistente.");
    }
}
