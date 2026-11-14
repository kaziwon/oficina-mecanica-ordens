using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IOrcamentoClienteSource
{
    Task<OrcamentoClienteOutput> Obter(VinculoOrcamentoClienteInput vinculo, CancellationToken cancellation);
    Task<OrcamentoClienteOutput> Responder(VinculoOrcamentoClienteInput vinculo, bool aprovado, string? motivo, CancellationToken cancellation);
}
