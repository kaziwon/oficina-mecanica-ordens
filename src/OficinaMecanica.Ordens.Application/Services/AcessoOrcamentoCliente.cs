using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;

namespace OficinaMecanica.Ordens.Application.Services;

public class AcessoOrcamentoCliente(IClienteGateway clientes, IOrdemServicoGateway ordens, ISagaAberturaGateway sagas)
{
    public VinculoOrcamentoClienteInput? Obter(Guid clienteId, Guid ordemId)
    {
        if (clienteId == Guid.Empty || clientes.ObterPorId(clienteId) is not { Ativo: true })
            throw new AcessoNegadoException("Acesso do cliente indisponivel.");
        if (ordens.ObterPorIdDoCliente(ordemId, clienteId) is null) return null;
        var saga = sagas.ObterPorOrdemServicoId(ordemId);
        if (saga?.OrcamentoId is null) return null;
        if (saga.OrdemServicoId != ordemId || saga.ExecucaoId is null || saga.PedidoOrcamentoId is null || saga.ValorOrcamento is null)
            throw new IntegracaoOrcamentoException("Vinculo do orcamento ainda indisponivel.");
        return new(saga.OrcamentoId.Value, ordemId, saga.ExecucaoId.Value, saga.Id,
            saga.PedidoOrcamentoId.Value, saga.ValorOrcamento.Value, saga.CorrelationId);
    }
}
