using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.Veiculos;

public class AtualizarVeiculoUseCase
{
    private const string PlacaInvalida = "A placa informada é inválida.";
    private const string PlacaJaCadastrada = "Ja existe um veiculo cadastrado com esta placa.";
    private const string ClienteNaoEncontrado = "O cliente informado nao foi encontrado.";
    private readonly IVeiculoGateway _veiculoGateway;
    private readonly IPlacaVeiculoValidatorGateway _placaVeiculoValidatorGateway;

    public AtualizarVeiculoUseCase(
        IVeiculoGateway veiculoGateway,
        IPlacaVeiculoValidatorGateway placaVeiculoValidatorGateway)
    {
        _veiculoGateway = veiculoGateway;
        _placaVeiculoValidatorGateway = placaVeiculoValidatorGateway;
    }

    public VeiculoOutput? Executar(Guid id, VeiculoInput input)
    {
        var placa = ValidarPlaca(input.Placa);

        if (!_veiculoGateway.ClienteExiste(input.ClienteId))
        {
            throw new InvalidOperationException(ClienteNaoEncontrado);
        }

        if (_veiculoGateway.ExistePorPlacaExcetoId(placa, id))
        {
            throw new InvalidOperationException(PlacaJaCadastrada);
        }

        var veiculo = new Veiculo
        {
            Id = id,
            ClienteId = input.ClienteId,
            Placa = placa,
            Marca = input.Marca,
            Modelo = input.Modelo,
            Ano = input.Ano
        };

        var veiculoAtualizado = _veiculoGateway.Atualizar(veiculo);

        return veiculoAtualizado is null ? null : VeiculoOutputMapper.Mapear(veiculoAtualizado);
    }

    private string ValidarPlaca(string placa)
    {
        if (!_placaVeiculoValidatorGateway.EhValida(placa))
        {
            throw new ValidacaoException(PlacaInvalida);
        }

        return _placaVeiculoValidatorGateway.Normalizar(placa);
    }
}
