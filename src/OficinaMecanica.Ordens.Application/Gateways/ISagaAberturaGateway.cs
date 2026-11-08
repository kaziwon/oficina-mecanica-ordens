using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Gateways;

public interface ISagaAberturaGateway
{
    SagaAbertura? ObterPorId(Guid id);
    SagaAbertura? ObterPorOrdemServicoId(Guid id);
    bool FoiProcessada(Guid messageId);
    void Salvar(SagaAbertura saga, Guid? mensagemRecebidaId, MensagemIntegracao? mensagemSaida,
        bool cancelarOrdem = false, StatusOrdemServico? novoStatus = null, DateTime? registradoEm = null);
}
