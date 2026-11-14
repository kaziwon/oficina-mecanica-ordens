using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class CancelarReparoPagoUseCase
{
    private readonly ISagaAberturaGateway _sagas;
    public CancelarReparoPagoUseCase(ISagaAberturaGateway sagas) { _sagas = sagas; }
    public SagaAberturaOutput? Executar(Guid ordemId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 1000 || motivo.Any(char.IsControl))
            throw new ValidacaoException("Informe um motivo de cancelamento com ate 1000 caracteres, sem controles.");
        var saga = _sagas.ObterPorOrdemServicoId(ordemId);
        if (saga is null) return null;
        if (saga.SolicitarCompensacaoPaga(motivo.Trim())) _sagas.Salvar(saga, null, MensagensCompensacao.Cancelar(saga));
        return SagaAberturaOutputMapper.Mapear(saga);
    }
}
