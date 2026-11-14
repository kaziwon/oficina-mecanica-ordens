using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.UnitTests.Fakes;

namespace OficinaMecanica.Ordens.UnitTests;

public class MinhasOrdensServicoUseCaseTests
{
    [Fact]
    public void Listagem_SomentePropriasInclusiveEncerradasMaisNovasPrimeiro()
    {
        var clientes = new ClienteGatewayFake(); var id = Guid.NewGuid(); clientes.Clientes.Add(new Cliente { Id = id });
        var ordens = new OrdemServicoGatewayFake(); var agora = DateTime.UtcNow;
        foreach (var status in Enum.GetValues<StatusOrdemServico>())
            ordens.Ordens.Add(new OrdemServico { Id = Guid.NewGuid(), ClienteId = id, Status = status, CriadaEm = agora.AddMinutes((int)status) });
        ordens.Ordens.Add(new OrdemServico { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), CriadaEm = agora.AddDays(1) });
        var lista = new ObterMinhasOrdensServicoUseCase(clientes, ordens).Executar(id);
        Assert.Equal(Enum.GetValues<StatusOrdemServico>().Length, lista.Count); Assert.All(lista, item => Assert.Equal(id, item.ClienteId));
        Assert.True(lista[0].CriadaEm >= lista[^1].CriadaEm); Assert.Contains(lista, item => item.Status == StatusOrdemServico.Entregue);
        Assert.Contains(lista, item => item.Status == StatusOrdemServico.Cancelada);
    }

    [Fact]
    public void ConsultaPorId_NaoDistingueOrdemAlheiaDeInexistente()
    {
        var clientes = new ClienteGatewayFake(); var id = Guid.NewGuid(); clientes.Clientes.Add(new Cliente { Id = id });
        var ordens = new OrdemServicoGatewayFake(); var propria = new OrdemServico { Id = Guid.NewGuid(), ClienteId = id };
        var alheia = new OrdemServico { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid() }; ordens.Ordens.AddRange([propria, alheia]);
        var useCase = new ObterMinhaOrdemServicoPorIdUseCase(clientes, ordens);
        Assert.Equal(propria.Id, useCase.Executar(id, propria.Id)!.Id); Assert.Null(useCase.Executar(id, alheia.Id)); Assert.Null(useCase.Executar(id, Guid.NewGuid()));
    }

    [Fact]
    public void ClienteDesativadoDepoisDoLogin_PerdeAcessoMesmoComIdentificadorConhecido()
    {
        var clientes = new ClienteGatewayFake(); var id = Guid.NewGuid(); clientes.Clientes.Add(new Cliente { Id = id });
        var ordens = new OrdemServicoGatewayFake(); var listar = new ObterMinhasOrdensServicoUseCase(clientes, ordens);
        Assert.Empty(listar.Executar(id)); clientes.Remover(id);
        Assert.Throws<AcessoNegadoException>(() => listar.Executar(id));
        Assert.Throws<AcessoNegadoException>(() => new ObterMinhaOrdemServicoPorIdUseCase(clientes, ordens).Executar(id, Guid.NewGuid()));
        Assert.Throws<AcessoNegadoException>(() => listar.Executar(Guid.Empty));
    }
}
