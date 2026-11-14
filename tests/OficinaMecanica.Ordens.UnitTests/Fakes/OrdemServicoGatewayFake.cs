using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public sealed class OrdemServicoGatewayFake : IOrdemServicoGateway
{
    public List<OrdemServico> Ordens { get; } = [];
    public bool RecusarEntrega { get; set; }
    public int TentativasEntrega { get; private set; }
    public OrdemServico Adicionar(OrdemServico ordem, SagaAbertura saga,
        Application.Messaging.MensagemIntegracao mensagem)
    { Ordens.Add(ordem); return ordem; }
    public OrdemServico? ObterPorId(Guid id)
    {
        var ordem = Ordens.FirstOrDefault(item => item.Id == id);
        return ordem is null ? null : Copiar(ordem);
    }
    public bool TentarRegistrarEntrega(OrdemServico ordem)
    {
        TentativasEntrega++;
        var indice = Ordens.FindIndex(item => item.Id == ordem.Id && item.Status == StatusOrdemServico.Finalizada);
        if (RecusarEntrega || indice < 0) return false;
        Ordens[indice] = Copiar(ordem);
        return true;
    }
    public OrdemServico? ObterPorIdDoCliente(Guid id, Guid clienteId)
    {
        var ordem = ObterPorId(id);
        return ordem?.ClienteId == clienteId ? ordem : null;
    }
    public List<OrdemServico> ObterDoCliente(Guid clienteId) => Ordens.Where(item => item.ClienteId == clienteId)
        .OrderByDescending(item => item.CriadaEm).ThenBy(item => item.Id).Select(Copiar).ToList();
    public List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro) => Ordens;

    private static OrdemServico Copiar(OrdemServico ordem) => new()
    {
        Id = ordem.Id,
        ClienteId = ordem.ClienteId,
        VeiculoId = ordem.VeiculoId,
        DescricaoSolicitacao = ordem.DescricaoSolicitacao,
        Status = ordem.Status,
        CriadaEm = ordem.CriadaEm,
        Cliente = ordem.Cliente,
        Veiculo = ordem.Veiculo,
        Historico = ordem.Historico.Select(item => new HistoricoStatusOrdemServico
        { Id = item.Id, OrdemServicoId = item.OrdemServicoId, Status = item.Status, RegistradoEm = item.RegistradoEm }).ToList()
    };
}
