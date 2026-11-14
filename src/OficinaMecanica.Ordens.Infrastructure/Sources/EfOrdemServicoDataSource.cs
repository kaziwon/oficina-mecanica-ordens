using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence.Models;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class EfOrdemServicoDataSource : IOrdemServicoDataSource
{
    private readonly AppDbContext _context;

    public EfOrdemServicoDataSource(AppDbContext context)
    {
        _context = context;
    }

    public OrdemServico Adicionar(OrdemServico ordemServico, SagaAbertura saga, MensagemIntegracao mensagem)
    {
        _context.OrdensServico.Add(ordemServico);
        _context.SagasAbertura.Add(saga);
        _context.Outbox.Add(new MensagemOutbox
        {
            Id = mensagem.MessageId,
            SagaId = saga.Id,
            Body = MensagemSerializer.Serializar(mensagem),
            CriadaEm = mensagem.OccurredAt
        });
        _context.SaveChanges();
        return ordemServico;
    }

    public OrdemServico? ObterPorId(Guid id)
    {
        return _context.OrdensServico.AsNoTracking().Include(item => item.Historico)
            .FirstOrDefault(item => item.Id == id);
    }

    public bool TentarRegistrarEntrega(OrdemServico ordemServico)
    {
        var entrega = ordemServico.Historico.Single(item => item.Status == StatusOrdemServico.Entregue);
        if (ordemServico.Status != StatusOrdemServico.Entregue || entrega.OrdemServicoId != ordemServico.Id)
            throw new InvalidOperationException("O registro da entrega nao corresponde a OS.");
        using var transaction = _context.Database.BeginTransaction();
        // A condicao de status permite que apenas uma requisicao confirme a entrega.
        var atualizadas = _context.OrdensServico
            .Where(item => item.Id == ordemServico.Id && item.Status == StatusOrdemServico.Finalizada)
            .ExecuteUpdate(update => update.SetProperty(item => item.Status, StatusOrdemServico.Entregue));
        if (atualizadas == 0)
        {
            transaction.Rollback();
            return false;
        }
        _context.HistoricoStatus.Add(entrega);
        _context.SaveChanges();
        transaction.Commit();
        return true;
    }

    public OrdemServico? ObterPorIdDoCliente(Guid id, Guid clienteId)
        => _context.OrdensServico.AsNoTracking().Include(item => item.Historico)
            .FirstOrDefault(item => item.Id == id && item.ClienteId == clienteId && item.Cliente!.Ativo);

    public List<OrdemServico> ObterDoCliente(Guid clienteId)
        => _context.OrdensServico.AsNoTracking().Include(item => item.Historico)
            .Where(item => item.ClienteId == clienteId && item.Cliente!.Ativo)
            .OrderByDescending(item => item.CriadaEm).ThenBy(item => item.Id).ToList();

    public List<OrdemServico> ObterTodas(StatusOrdemServico? status, bool maisNovasPrimeiro)
    {
        var query = _context.OrdensServico.AsNoTracking().Include(item => item.Historico)
            .Where(item => item.Status != StatusOrdemServico.Finalizada
                && item.Status != StatusOrdemServico.Entregue && item.Status != StatusOrdemServico.Cancelada);

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var porStatus = query.OrderBy(item => item.Status == StatusOrdemServico.EmExecucao ? 0
            : item.Status == StatusOrdemServico.AguardandoExecucao ? 1
            : item.Status == StatusOrdemServico.PagamentoConfirmado ? 2
            : item.Status == StatusOrdemServico.AguardandoPagamento ? 3
            : item.Status == StatusOrdemServico.AguardandoAprovacao ? 4
            : item.Status == StatusOrdemServico.EmDiagnostico ? 5 : 6);

        return (maisNovasPrimeiro ? porStatus.ThenByDescending(item => item.CriadaEm)
            : porStatus.ThenBy(item => item.CriadaEm)).ThenBy(item => item.Id).ToList();
    }
}
