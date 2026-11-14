using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Services;
using OficinaMecanica.Ordens.Application.Validators;

namespace OficinaMecanica.Ordens.Application.UseCases.Orcamentos;

public class ObterMeuOrcamentoUseCase(AcessoOrcamentoCliente acesso, IOrcamentoClienteGateway orcamentos)
{
    public async Task<OrcamentoClienteOutput?> Executar(Guid clienteId, Guid ordemId, CancellationToken cancellation)
    {
        var vinculo = acesso.Obter(clienteId, ordemId);
        if (vinculo is null) return null;
        var output = await orcamentos.Obter(vinculo, cancellation);
        OrcamentoClienteValidator.Validar(output, vinculo);
        return output;
    }
}
