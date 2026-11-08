using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class CancelarEntradaExecucaoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public CancelarEntradaExecucaoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public SagaAberturaOutput? Executar(Guid ordemServicoId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 1000)
            throw new ValidacaoException("Informe um motivo com ate 1000 caracteres.");
        var saga = _sagas.ObterPorOrdemServicoId(ordemServicoId);
        if (saga is null) return null;
        if (saga.SolicitarCancelamento(motivo.Trim())) _sagas.Salvar(saga, null, MensagensAbertura.Cancelar(saga));
        return SagaAberturaOutputMapper.Mapear(saga);
    }
}
