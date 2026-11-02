using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Inputs.OrdensServico;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class OrdensServicoCleanController
{
    private readonly CriarOrdemServicoUseCase _criar;
    private readonly ObterOrdemServicoPorIdUseCase _obter;
    private readonly ObterTodasOrdensServicoUseCase _listar;

    public OrdensServicoCleanController(
        CriarOrdemServicoUseCase criar,
        ObterOrdemServicoPorIdUseCase obter,
        ObterTodasOrdensServicoUseCase listar)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
    }

    public OrdemServicoResponseDto Criar(OrdemServicoRequestDto request)
    {
        var cliente = request.Cliente is null ? null : new CriarClienteInput(
            request.Cliente.Nome, request.Cliente.CpfCnpj, request.Cliente.Email, request.Cliente.Telefone);
        var veiculo = request.Veiculo is null ? null : new CriarVeiculoDaOrdemInput(
            request.Veiculo.Placa, request.Veiculo.Marca, request.Veiculo.Modelo, request.Veiculo.Ano);
        var input = new CriarOrdemServicoInput(
            request.CpfCnpj, request.VeiculoId, cliente, veiculo, request.DescricaoSolicitacao);

        return OrdemServicoPresenter.Apresentar(_criar.Executar(input));
    }

    public OrdemServicoResponseDto? ObterPorId(Guid id)
    {
        var output = _obter.Executar(id);
        return output is null ? null : OrdemServicoPresenter.Apresentar(output);
    }

    public List<OrdemServicoResponseDto> ObterTodas(StatusOrdemServico? status, string? ordenacao)
        => _listar.Executar(status, ordenacao).Select(OrdemServicoPresenter.Apresentar).ToList();
}
