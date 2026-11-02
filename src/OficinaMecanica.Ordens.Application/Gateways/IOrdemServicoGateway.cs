using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IOrdemServicoGateway
{
    OrdemServico Adicionar(OrdemServico ordemServico);
    OrdemServico? ObterPorId(Guid id);
    List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro);
}
