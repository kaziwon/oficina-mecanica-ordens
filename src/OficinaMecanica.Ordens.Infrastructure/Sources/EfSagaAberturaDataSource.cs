using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Persistence.Models;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class EfSagaAberturaDataSource : ISagaAberturaDataSource
{
    private readonly AppDbContext _database;
    public EfSagaAberturaDataSource(AppDbContext database) { _database = database; }
    public SagaAbertura? ObterPorId(Guid id) => _database.SagasAbertura.SingleOrDefault(item => item.Id == id);
    public SagaAbertura? ObterPorOrdemServicoId(Guid id) => _database.SagasAbertura.SingleOrDefault(item => item.OrdemServicoId == id);
    public bool FoiProcessada(Guid messageId) => _database.Inbox.Any(item => item.Id == messageId);

    public void Salvar(SagaAbertura saga, Guid? mensagemRecebidaId, MensagemIntegracao? mensagemSaida,
        bool cancelarOrdem, StatusOrdemServico? novoStatus = null, DateTime? registradoEm = null)
    {
        if (novoStatus.HasValue)
        {
            var ordem = _database.OrdensServico.Include(item => item.Historico).Single(item => item.Id == saga.OrdemServicoId);
            if (novoStatus == StatusOrdemServico.EmDiagnostico) ordem.RegistrarDiagnostico(saga.DiagnosticoIniciadoEm!.Value);
            else if (novoStatus == StatusOrdemServico.AguardandoAprovacao)
                ordem.AguardarAprovacao(saga.DiagnosticoIniciadoEm!.Value, registradoEm!.Value);
            else if (novoStatus == StatusOrdemServico.AguardandoPagamento)
                ordem.AguardarPagamento(saga.DiagnosticoIniciadoEm!.Value, saga.OrcamentoCriadoEm!.Value, saga.OrcamentoRespondidoEm!.Value);
            else if (novoStatus == StatusOrdemServico.Cancelada)
                ordem.CancelarPorRecusaOrcamento(registradoEm!.Value);
            else if (novoStatus == StatusOrdemServico.PagamentoConfirmado)
                ordem.ConfirmarPagamento(registradoEm!.Value);
            else throw new InvalidOperationException("Transicao de integracao desconhecida.");
        }
        if (cancelarOrdem)
            _database.OrdensServico.Include(item => item.Historico).Single(item => item.Id == saga.OrdemServicoId).CancelarEntrada();
        if (mensagemRecebidaId.HasValue)
            _database.Inbox.Add(new MensagemInbox { Id = mensagemRecebidaId.Value, SagaId = saga.Id, ProcessadaEm = DateTime.UtcNow });
        if (mensagemSaida is not null)
            _database.Outbox.Add(new MensagemOutbox
            {
                Id = mensagemSaida.MessageId,
                SagaId = saga.Id,
                Body = MensagemSerializer.Serializar(mensagemSaida),
                CriadaEm = mensagemSaida.OccurredAt
            });
        // Uma unica transacao grava o estado, a deduplicacao e qualquer comando de compensacao.
        _database.SaveChanges();
    }
}
