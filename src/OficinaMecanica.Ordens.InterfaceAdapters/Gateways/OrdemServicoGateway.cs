using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

public class OrdemServicoGateway : IOrdemServicoGateway
{
    private readonly IOrdemServicoDataSource _source;

    public OrdemServicoGateway(IOrdemServicoDataSource source)
    {
        _source = source;
    }

    public OrdemServico Adicionar(OrdemServico ordemServico, SagaAbertura saga, MensagemIntegracao mensagem)
        => _source.Adicionar(ordemServico, saga, mensagem);
    public OrdemServico? ObterPorId(Guid id) => _source.ObterPorId(id);
    public OrdemServico? ObterPorIdDoCliente(Guid id, Guid clienteId) => _source.ObterPorIdDoCliente(id, clienteId);
    public List<OrdemServico> ObterDoCliente(Guid clienteId) => _source.ObterDoCliente(clienteId);
    public bool TentarRegistrarEntrega(OrdemServico ordemServico) => _source.TentarRegistrarEntrega(ordemServico);
    public List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro)
        => _source.ObterTodas(status, maisNovasPrimeiro);
}
