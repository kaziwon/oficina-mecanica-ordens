using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence.Models;

namespace OficinaMecanica.Ordens.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();
    public DbSet<HistoricoStatusOrdemServico> HistoricoStatus => Set<HistoricoStatusOrdemServico>();
    public DbSet<SagaAbertura> SagasAbertura => Set<SagaAbertura>();
    public DbSet<MensagemOutbox> Outbox => Set<MensagemOutbox>();
    public DbSet<MensagemInbox> Inbox => Set<MensagemInbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoricoStatusOrdemServico>().Property(item => item.Id).ValueGeneratedNever();
        modelBuilder.Entity<SagaAbertura>(entity =>
        {
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Motivo).HasMaxLength(1000);
            entity.Property(item => item.Versao).IsConcurrencyToken();
            entity.Property(item => item.ValorOrcamento).HasPrecision(11, 2);
            entity.HasOne<OrdemServico>().WithOne().HasForeignKey<SagaAbertura>(item => item.OrdemServicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MensagemOutbox>(entity =>
        {
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Body).IsRequired();
            entity.HasIndex(item => new { item.PublicadaEm, item.CriadaEm });
            entity.HasOne<SagaAbertura>().WithMany().HasForeignKey(item => item.SagaId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MensagemInbox>(entity =>
        {
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.HasOne<SagaAbertura>().WithMany().HasForeignKey(item => item.SagaId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.Property(item => item.Nome).HasMaxLength(150).IsRequired();
            entity.Property(item => item.CpfCnpj).HasMaxLength(14).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.Telefone).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => item.CpfCnpj).IsUnique();
            entity.HasMany(item => item.Veiculos).WithOne(item => item.Cliente)
                .HasForeignKey(item => item.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.OrdensServico).WithOne(item => item.Cliente)
                .HasForeignKey(item => item.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Veiculo>(entity =>
        {
            entity.Property(item => item.Placa).HasMaxLength(7).IsRequired();
            entity.Property(item => item.Marca).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Modelo).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.Placa).IsUnique();
            entity.HasMany(item => item.OrdensServico).WithOne(item => item.Veiculo)
                .HasForeignKey(item => item.VeiculoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrdemServico>(entity =>
        {
            entity.Property(item => item.DescricaoSolicitacao).HasMaxLength(1000).IsRequired();
            entity.HasIndex(item => new { item.Status, item.CriadaEm });
            entity.HasMany(item => item.Historico).WithOne()
                .HasForeignKey(item => item.OrdemServicoId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
