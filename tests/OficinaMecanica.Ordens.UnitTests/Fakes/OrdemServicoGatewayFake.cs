using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public sealed class OrdemServicoGatewayFake : IOrdemServicoGateway
{
    public List<OrdemServico> Ordens { get; } = [];
    public OrdemServico Adicionar(OrdemServico ordem) { Ordens.Add(ordem); return ordem; }
    public OrdemServico? ObterPorId(Guid id) => Ordens.FirstOrDefault(item => item.Id == id);
    public List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro) => Ordens;
}
