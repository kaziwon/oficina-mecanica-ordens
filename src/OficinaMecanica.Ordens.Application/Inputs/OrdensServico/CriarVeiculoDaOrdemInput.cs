namespace OficinaMecanica.Ordens.Application.Inputs.OrdensServico;

public class CriarVeiculoDaOrdemInput
{
    public CriarVeiculoDaOrdemInput(string placa, string marca, string modelo, int ano)
    {
        Placa = placa;
        Marca = marca;
        Modelo = modelo;
        Ano = ano;
    }

    public string Placa { get; }
    public string Marca { get; }
    public string Modelo { get; }
    public int Ano { get; }
}
