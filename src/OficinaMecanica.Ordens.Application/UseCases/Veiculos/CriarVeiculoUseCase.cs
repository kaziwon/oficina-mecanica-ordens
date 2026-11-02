using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.Application.Mappers.Veiculos;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.UseCases.Veiculos;

public class CriarVeiculoUseCase
{
    private const string PlacaInvalida = "A placa informada é inválida.";
    private const string PlacaJaCadastrada = "Ja existe um veiculo cadastrado com esta placa.";
    private const string ClienteNaoEncontrado = "O cliente informado nao foi encontrado.";
    private readonly IVeiculoGateway _veiculoGateway;
    private readonly IPlacaVeiculoValidatorGateway _placaVeiculoValidatorGateway;

    public CriarVeiculoUseCase(
        IVeiculoGateway veiculoGateway,
        IPlacaVeiculoValidatorGateway placaVeiculoValidatorGateway)
    {
        _veiculoGateway = veiculoGateway;
        _placaVeiculoValidatorGateway = placaVeiculoValidatorGateway;
    }

    public VeiculoOutput Executar(VeiculoInput input)
    {
        var placa = ValidarPlaca(input.Placa);
        ValidarClienteEPlaca(input.ClienteId, placa);

        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(),
            ClienteId = input.ClienteId,
            Placa = placa,
            Marca = input.Marca,
            Modelo = input.Modelo,
            Ano = input.Ano
        };

        return VeiculoOutputMapper.Mapear(_veiculoGateway.Adicionar(veiculo));
    }

    private void ValidarClienteEPlaca(Guid clienteId, string placa)
    {
        if (!_veiculoGateway.ClienteExiste(clienteId))
        {
            throw new InvalidOperationException(ClienteNaoEncontrado);
        }

        if (_veiculoGateway.ExistePorPlaca(placa))
        {
            throw new InvalidOperationException(PlacaJaCadastrada);
        }
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
