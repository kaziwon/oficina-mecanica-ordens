using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Mappers.Veiculos;

internal static class VeiculoOutputMapper
{
    public static VeiculoOutput Mapear(Veiculo veiculo)
    {
        return new VeiculoOutput
        {
            Id = veiculo.Id,
            ClienteId = veiculo.ClienteId,
            Placa = veiculo.Placa,
            Marca = veiculo.Marca,
            Modelo = veiculo.Modelo,
            Ano = veiculo.Ano
        };
    }
}
