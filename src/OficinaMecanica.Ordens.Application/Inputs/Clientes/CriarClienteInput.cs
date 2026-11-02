using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.Application.Mappers.Clientes;
namespace OficinaMecanica.Ordens.Application.Inputs.Clientes;

public class CriarClienteInput
{
    public CriarClienteInput(string nome, string cpfCnpj, string email, string telefone)
    {
        Nome = nome;
        CpfCnpj = cpfCnpj;
        Email = email;
        Telefone = telefone;
    }

    public string Nome { get; }
    public string CpfCnpj { get; }
    public string Email { get; }
    public string Telefone { get; }
}
