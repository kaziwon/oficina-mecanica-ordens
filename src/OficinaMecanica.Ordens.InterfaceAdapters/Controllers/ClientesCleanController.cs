using OficinaMecanica.Ordens.Application.Inputs.Clientes;
using OficinaMecanica.Ordens.Application.Outputs.Clientes;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using OficinaMecanica.Ordens.Application.UseCases.Clientes;
using OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class ClientesCleanController
{
    private readonly ObterTodosClientesUseCase _obterTodosClientesUseCase;
    private readonly ObterClientePorIdUseCase _obterClientePorIdUseCase;
    private readonly CriarClienteUseCase _criarClienteUseCase;
    private readonly AtualizarClienteUseCase _atualizarClienteUseCase;
    private readonly RemoverClienteUseCase _removerClienteUseCase;

    public ClientesCleanController(
        ObterTodosClientesUseCase obterTodosClientesUseCase,
        ObterClientePorIdUseCase obterClientePorIdUseCase,
        CriarClienteUseCase criarClienteUseCase,
        AtualizarClienteUseCase atualizarClienteUseCase,
        RemoverClienteUseCase removerClienteUseCase)
    {
        _obterTodosClientesUseCase = obterTodosClientesUseCase;
        _obterClientePorIdUseCase = obterClientePorIdUseCase;
        _criarClienteUseCase = criarClienteUseCase;
        _atualizarClienteUseCase = atualizarClienteUseCase;
        _removerClienteUseCase = removerClienteUseCase;
    }

    public List<ClienteResponseDto> ObterTodos()
    {
        return _obterTodosClientesUseCase
            .Executar()
            .Select(ClientePresenter.Apresentar)
            .ToList();
    }

    public ClienteResponseDto? ObterPorId(Guid id)
    {
        var cliente = _obterClientePorIdUseCase.Executar(id);

        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public ClienteResponseDto Criar(ClienteRequestDto clienteRequestDto)
    {
        var input = new CriarClienteInput(
            clienteRequestDto.Nome,
            clienteRequestDto.CpfCnpj,
            clienteRequestDto.Email,
            clienteRequestDto.Telefone);

        return ClientePresenter.Apresentar(_criarClienteUseCase.Executar(input));
    }

    public ClienteResponseDto? Atualizar(Guid id, ClienteRequestDto clienteRequestDto)
    {
        var input = new AtualizarClienteInput(
            id,
            clienteRequestDto.Nome,
            clienteRequestDto.CpfCnpj,
            clienteRequestDto.Email,
            clienteRequestDto.Telefone);
        var clienteAtualizado = _atualizarClienteUseCase.Executar(input);

        return clienteAtualizado is null ? null : ClientePresenter.Apresentar(clienteAtualizado);
    }

    public bool Remover(Guid id)
    {
        return _removerClienteUseCase.Executar(id);
    }

}
