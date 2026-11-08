using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IOutboxDataSource
{
    List<MensagemIntegracao> ObterPendentes(int limite);
    void ConfirmarPublicacao(Guid messageId);
}
