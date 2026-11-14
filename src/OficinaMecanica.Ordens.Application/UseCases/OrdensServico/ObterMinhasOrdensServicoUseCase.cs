using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class ObterMinhasOrdensServicoUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly IOrdemServicoGateway _ordens;
    public ObterMinhasOrdensServicoUseCase(IClienteGateway clientes, IOrdemServicoGateway ordens)
    { _clientes = clientes; _ordens = ordens; }

    public List<OrdemServicoOutput> Executar(Guid clienteId)
    {
        if (clienteId == Guid.Empty || _clientes.ObterPorId(clienteId) is not { Ativo: true })
            throw new AcessoNegadoException("Acesso do cliente indisponivel.");
        return _ordens.ObterDoCliente(clienteId).Select(OrdemServicoOutputMapper.Mapear).ToList();
    }
}
