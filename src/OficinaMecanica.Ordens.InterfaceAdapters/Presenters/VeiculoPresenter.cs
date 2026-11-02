using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class VeiculoPresenter
{
    public static VeiculoResponseDto Apresentar(VeiculoOutput veiculo)
        => new()
        {
            Id = veiculo.Id,
            ClienteId = veiculo.ClienteId,
            Placa = veiculo.Placa,
            Marca = veiculo.Marca,
            Modelo = veiculo.Modelo,
            Ano = veiculo.Ano
        };
}
