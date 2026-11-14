using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class OrcamentoClienteGateway(IOrcamentoClienteSource source) : IOrcamentoClienteGateway
{
    public Task<OrcamentoClienteOutput> Obter(VinculoOrcamentoClienteInput vinculo, CancellationToken cancellation) => source.Obter(vinculo, cancellation);
    public Task<OrcamentoClienteOutput> Responder(VinculoOrcamentoClienteInput vinculo, bool aprovado, string? motivo, CancellationToken cancellation)
        => source.Responder(vinculo, aprovado, motivo, cancellation);
}
