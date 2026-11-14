using OficinaMecanica.Ordens.Application.UseCases.Orcamentos;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class MeuOrcamentoCleanController(ObterMeuOrcamentoUseCase obter, ResponderMeuOrcamentoUseCase responder)
{
    public async Task<OrcamentoClienteResponseDto?> Obter(Guid clienteId, Guid ordemId, CancellationToken cancellation)
    {
        var output = await obter.Executar(clienteId, ordemId, cancellation);
        return output is null ? null : OrcamentoClientePresenter.Apresentar(output);
    }
    public async Task<OrcamentoClienteResponseDto?> Responder(Guid clienteId, Guid ordemId, bool aprovado, string? motivo, CancellationToken cancellation)
    {
        var output = await responder.Executar(clienteId, ordemId, aprovado, motivo, cancellation);
        return output is null ? null : OrcamentoClientePresenter.Apresentar(output);
    }
}
