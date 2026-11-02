using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IOrdemServicoDataSource
{
    OrdemServico Adicionar(OrdemServico ordemServico);
    OrdemServico? ObterPorId(Guid id);
    List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro);
}
