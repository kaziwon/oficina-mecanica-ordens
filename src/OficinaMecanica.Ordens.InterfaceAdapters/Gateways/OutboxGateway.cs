using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class OutboxGateway : IOutboxGateway
{
    private readonly IOutboxDataSource _source;
    public OutboxGateway(IOutboxDataSource source) { _source = source; }
    public List<MensagemIntegracao> ObterPendentes(int limite) => _source.ObterPendentes(limite);
    public void ConfirmarPublicacao(Guid messageId) => _source.ConfirmarPublicacao(messageId);
}
