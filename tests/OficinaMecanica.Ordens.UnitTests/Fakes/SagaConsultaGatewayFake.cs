using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public class SagaConsultaGatewayFake : ISagaAberturaGateway
{
    public List<SagaAbertura> Sagas { get; } = [];
    public SagaAbertura? ObterPorId(Guid id) => Sagas.SingleOrDefault(item => item.Id == id);
    public SagaAbertura? ObterPorOrdemServicoId(Guid id) => Sagas.SingleOrDefault(item => item.OrdemServicoId == id);
    public bool FoiProcessada(Guid id) => false;
    public void Salvar(SagaAbertura saga, Guid? mensagemRecebidaId, MensagemIntegracao? mensagemSaida,
        bool cancelarOrdem = false, StatusOrdemServico? novoStatus = null, DateTime? registradoEm = null)
        => throw new NotSupportedException("Atendimento do cliente nao altera a Saga diretamente.");
}
