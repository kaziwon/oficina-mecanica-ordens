namespace OficinaMecanica.Ordens.Domain.Entities;

public class Veiculo
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Ano { get; set; }
    public bool Ativo { get; set; } = true;
    public Cliente? Cliente { get; set; }
    public ICollection<OrdemServico> OrdensServico { get; set; } = [];
}
