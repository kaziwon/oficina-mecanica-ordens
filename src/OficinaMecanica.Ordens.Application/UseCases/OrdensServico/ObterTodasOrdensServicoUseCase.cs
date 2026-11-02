using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ObterTodasOrdensServicoUseCase
{
    private readonly IOrdemServicoGateway _ordens;

    public ObterTodasOrdensServicoUseCase(IOrdemServicoGateway ordens)
    {
        _ordens = ordens;
    }

    public List<OrdemServicoOutput> Executar(StatusOrdemServico? status, string? ordenacao)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ValidacaoException("Status invalido.");
        }

        var maisNovas = ordenacao?.Trim().ToLowerInvariant() switch
        {
            null or "" or "maisantigo" => false,
            "maisnovo" => true,
            _ => throw new ValidacaoException("Use maisAntigo ou maisNovo para ordenar.")
        };
        return _ordens.ObterTodas(status, maisNovas).Select(OrdemServicoOutputMapper.Mapear).ToList();
    }
}
