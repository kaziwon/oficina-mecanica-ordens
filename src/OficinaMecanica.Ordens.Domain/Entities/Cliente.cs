namespace OficinaMecanica.Ordens.Domain.Entities;

public class Cliente
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfCnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public ICollection<Veiculo> Veiculos { get; set; } = [];
    public ICollection<OrdemServico> OrdensServico { get; set; } = [];
}
