using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IOrcamentoClienteGateway
{
    Task<OrcamentoClienteOutput> Obter(VinculoOrcamentoClienteInput vinculo, CancellationToken cancellation);
    Task<OrcamentoClienteOutput> Responder(VinculoOrcamentoClienteInput vinculo, bool aprovado, string? motivo, CancellationToken cancellation);
}
