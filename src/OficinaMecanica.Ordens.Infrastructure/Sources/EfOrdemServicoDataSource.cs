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
            : item.Status == StatusOrdemServico.AguardandoPagamento ? 1
            : item.Status == StatusOrdemServico.AguardandoAprovacao ? 2
            : item.Status == StatusOrdemServico.EmDiagnostico ? 3 : 4);

        return (maisNovasPrimeiro ? porStatus.ThenByDescending(item => item.CriadaEm)
            : porStatus.ThenBy(item => item.CriadaEm)).ThenBy(item => item.Id).ToList();
    }
}
