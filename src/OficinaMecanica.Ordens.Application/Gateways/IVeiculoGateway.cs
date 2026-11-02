using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Gateways;

public interface IVeiculoGateway
{
    List<Veiculo> ObterTodos();
    Veiculo? ObterPorId(Guid id);
    bool ExistePorPlaca(string placa);
    bool ExistePorPlacaExcetoId(string placa, Guid id);
    bool ClienteExiste(Guid clienteId);
    Veiculo Adicionar(Veiculo veiculo);
    Veiculo? Atualizar(Veiculo veiculo);
    bool Remover(Guid id);
}
