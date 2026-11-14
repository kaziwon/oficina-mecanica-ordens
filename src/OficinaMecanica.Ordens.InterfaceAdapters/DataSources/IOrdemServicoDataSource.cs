using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IOrdemServicoDataSource
{
    OrdemServico Adicionar(OrdemServico ordemServico, SagaAbertura saga, MensagemIntegracao mensagem);
    OrdemServico? ObterPorId(Guid id);
    OrdemServico? ObterPorIdDoCliente(Guid id, Guid clienteId);
    List<OrdemServico> ObterDoCliente(Guid clienteId);
    bool TentarRegistrarEntrega(OrdemServico ordemServico);
    List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro);
}
