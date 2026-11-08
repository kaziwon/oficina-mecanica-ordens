using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IOutboxGateway
{
    List<MensagemIntegracao> ObterPendentes(int limite);
    void ConfirmarPublicacao(Guid messageId);
}
