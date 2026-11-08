using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IOrdemServicoDataSource
{
    OrdemServico Adicionar(OrdemServico ordemServico, SagaAbertura saga, MensagemIntegracao mensagem);
    OrdemServico? ObterPorId(Guid id);
    List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro);
}
