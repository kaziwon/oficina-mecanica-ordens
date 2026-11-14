using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ObterMinhaOrdemServicoPorIdUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly IOrdemServicoGateway _ordens;
    public ObterMinhaOrdemServicoPorIdUseCase(IClienteGateway clientes, IOrdemServicoGateway ordens)
    { _clientes = clientes; _ordens = ordens; }

    public OrdemServicoOutput? Executar(Guid clienteId, Guid ordemId)
    {
        if (clienteId == Guid.Empty || _clientes.ObterPorId(clienteId) is not { Ativo: true })
            throw new AcessoNegadoException("Acesso do cliente indisponivel.");
        var ordem = _ordens.ObterPorIdDoCliente(ordemId, clienteId);
        return ordem is null ? null : OrdemServicoOutputMapper.Mapear(ordem);
    }
}
