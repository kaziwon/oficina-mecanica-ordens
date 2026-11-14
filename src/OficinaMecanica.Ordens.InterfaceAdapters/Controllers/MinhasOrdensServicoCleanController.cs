using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class MinhasOrdensServicoCleanController(ObterMinhasOrdensServicoUseCase listar, ObterMinhaOrdemServicoPorIdUseCase obter)
{
    public List<OrdemServicoResponseDto> ObterTodas(Guid clienteId)
        => listar.Executar(clienteId).Select(OrdemServicoPresenter.Apresentar).ToList();
    public OrdemServicoResponseDto? ObterPorId(Guid clienteId, Guid ordemId)
    {
        var output = obter.Executar(clienteId, ordemId);
        return output is null ? null : OrdemServicoPresenter.Apresentar(output);
    }
}
