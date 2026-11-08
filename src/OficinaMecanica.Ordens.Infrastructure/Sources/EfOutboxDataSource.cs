using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Messaging;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class EfOutboxDataSource : IOutboxDataSource
{
    private readonly AppDbContext _database;
    public EfOutboxDataSource(AppDbContext database) { _database = database; }
    public List<MensagemIntegracao> ObterPendentes(int limite) => _database.Outbox.AsNoTracking()
        .Where(item => item.PublicadaEm == null).OrderBy(item => item.CriadaEm).ThenBy(item => item.Id)
        .Take(limite).AsEnumerable().Select(item => MensagemSerializer.Desserializar(item.Body)).ToList();
    public void ConfirmarPublicacao(Guid messageId) => _database.Outbox
        .Where(item => item.Id == messageId && item.PublicadaEm == null)
        .ExecuteUpdate(update => update.SetProperty(item => item.PublicadaEm, DateTime.UtcNow));
}
