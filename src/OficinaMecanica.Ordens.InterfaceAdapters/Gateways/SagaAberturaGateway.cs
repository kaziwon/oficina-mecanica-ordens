using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class SagaAberturaGateway : ISagaAberturaGateway
{
    private readonly ISagaAberturaDataSource _source;
    public SagaAberturaGateway(ISagaAberturaDataSource source) { _source = source; }
    public SagaAbertura? ObterPorId(Guid id) => _source.ObterPorId(id);
    public SagaAbertura? ObterPorOrdemServicoId(Guid id) => _source.ObterPorOrdemServicoId(id);
    public bool FoiProcessada(Guid messageId) => _source.FoiProcessada(messageId);
    public void Salvar(SagaAbertura saga, Guid? mensagemRecebidaId, MensagemIntegracao? mensagemSaida, bool cancelarOrdem = false)
        => _source.Salvar(saga, mensagemRecebidaId, mensagemSaida, cancelarOrdem);
}
