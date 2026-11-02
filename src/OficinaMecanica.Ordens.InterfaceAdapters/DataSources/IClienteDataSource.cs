using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

public interface IClienteDataSource
{
    List<Cliente> ObterTodos();
    Cliente? ObterPorId(Guid id);
    Cliente? ObterPorCpfCnpj(string cpfCnpj);
    bool ExistePorCpfCnpj(string cpfCnpj);
    bool ExistePorCpfCnpjExcetoId(string cpfCnpj, Guid id);
    Cliente Adicionar(Cliente cliente);
    Cliente? Atualizar(Cliente cliente);
    bool Remover(Guid id);
}
