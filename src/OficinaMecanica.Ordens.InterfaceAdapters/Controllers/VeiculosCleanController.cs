using OficinaMecanica.Ordens.Application.Inputs.Veiculos;
using OficinaMecanica.Ordens.Application.Outputs.Veiculos;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.Application.UseCases.Veiculos;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class VeiculosCleanController
{
    private readonly ObterTodosVeiculosUseCase _obterTodosVeiculosUseCase;
    private readonly ObterVeiculoPorIdUseCase _obterVeiculoPorIdUseCase;
    private readonly CriarVeiculoUseCase _criarVeiculoUseCase;
    private readonly AtualizarVeiculoUseCase _atualizarVeiculoUseCase;
    private readonly RemoverVeiculoUseCase _removerVeiculoUseCase;

    public VeiculosCleanController(
        ObterTodosVeiculosUseCase obterTodosVeiculosUseCase,
        ObterVeiculoPorIdUseCase obterVeiculoPorIdUseCase,
        CriarVeiculoUseCase criarVeiculoUseCase,
        AtualizarVeiculoUseCase atualizarVeiculoUseCase,
        RemoverVeiculoUseCase removerVeiculoUseCase)
    {
        _obterTodosVeiculosUseCase = obterTodosVeiculosUseCase;
        _obterVeiculoPorIdUseCase = obterVeiculoPorIdUseCase;
        _criarVeiculoUseCase = criarVeiculoUseCase;
        _atualizarVeiculoUseCase = atualizarVeiculoUseCase;
        _removerVeiculoUseCase = removerVeiculoUseCase;
    }

    public List<VeiculoResponseDto> ObterTodos()
    {
        return _obterTodosVeiculosUseCase.Executar().Select(VeiculoPresenter.Apresentar).ToList();
    }

    public VeiculoResponseDto? ObterPorId(Guid id)
    {
        var veiculo = _obterVeiculoPorIdUseCase.Executar(id);

        return veiculo is null ? null : VeiculoPresenter.Apresentar(veiculo);
    }

    public VeiculoResponseDto Criar(VeiculoRequestDto veiculoRequestDto)
    {
        var input = new VeiculoInput(
            veiculoRequestDto.ClienteId,
            veiculoRequestDto.Placa,
            veiculoRequestDto.Marca,
            veiculoRequestDto.Modelo,
            veiculoRequestDto.Ano);

        return VeiculoPresenter.Apresentar(_criarVeiculoUseCase.Executar(input));
    }

    public VeiculoResponseDto? Atualizar(Guid id, VeiculoRequestDto veiculoRequestDto)
    {
        var input = new VeiculoInput(
            veiculoRequestDto.ClienteId,
            veiculoRequestDto.Placa,
            veiculoRequestDto.Marca,
            veiculoRequestDto.Modelo,
            veiculoRequestDto.Ano);
        var veiculo = _atualizarVeiculoUseCase.Executar(id, input);

        return veiculo is null ? null : VeiculoPresenter.Apresentar(veiculo);
    }

    public bool Remover(Guid id)
    {
        return _removerVeiculoUseCase.Executar(id);
    }

}
