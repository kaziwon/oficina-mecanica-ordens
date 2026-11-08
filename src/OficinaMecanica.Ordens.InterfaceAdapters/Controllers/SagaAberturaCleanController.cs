using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class SagaAberturaCleanController
{
    private readonly ObterSagaAberturaUseCase _obter;
    private readonly CancelarEntradaExecucaoUseCase _cancelar;
    public SagaAberturaCleanController(ObterSagaAberturaUseCase obter, CancelarEntradaExecucaoUseCase cancelar)
    {
        _obter = obter; _cancelar = cancelar;
    }
    public SagaAberturaResponseDto? Obter(Guid ordemServicoId)
    {
        var output = _obter.Executar(ordemServicoId);
        return output is null ? null : SagaAberturaPresenter.Apresentar(output);
    }
    public SagaAberturaResponseDto? Cancelar(Guid ordemServicoId, string motivo)
    {
        var output = _cancelar.Executar(ordemServicoId, motivo);
        return output is null ? null : SagaAberturaPresenter.Apresentar(output);
    }
}
