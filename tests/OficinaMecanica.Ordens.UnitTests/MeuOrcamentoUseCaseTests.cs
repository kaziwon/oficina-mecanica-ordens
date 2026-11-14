using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Services;
using OficinaMecanica.Ordens.Application.UseCases.Orcamentos;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.UnitTests.Fakes;

namespace OficinaMecanica.Ordens.UnitTests;

public class MeuOrcamentoUseCaseTests
{
    [Theory]
    [InlineData("alheia")]
    [InlineData("ausente")]
    [InlineData("sem-orcamento")]
    public async Task SemOrcamentoProprio_NaoChamaCobranca(string caso)
    {
        var (cliente, ordem, acesso, remoto, _, sagas) = Criar();
        if (caso == "alheia") ordem.ClienteId = Guid.NewGuid();
        if (caso == "sem-orcamento") sagas.Sagas[0].OrcamentoId = null;
        var id = caso == "ausente" ? Guid.NewGuid() : ordem.Id;
        Assert.Null(await new ObterMeuOrcamentoUseCase(acesso, remoto).Executar(cliente.Id, id, default));
        Assert.Null(await new ResponderMeuOrcamentoUseCase(acesso, remoto).Executar(cliente.Id, id, true, null, default));
        Assert.Equal(0, remoto.Chamadas);
    }
    [Fact]
    public async Task ClienteInativo_NaoChamaCobranca()
    {
        var (cliente, ordem, acesso, remoto, _, _) = Criar(); cliente.Ativo = false;
        await Assert.ThrowsAsync<AcessoNegadoException>(() => new ObterMeuOrcamentoUseCase(acesso, remoto).Executar(cliente.Id, ordem.Id, default));
        Assert.Equal(0, remoto.Chamadas);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decisao_UsaVinculosDoServidorSemAlterarSaga(bool aprovado)
    {
        var (cliente, ordem, acesso, remoto, _, sagas) = Criar(); var status = sagas.Sagas[0].Status;
        var output = await new ResponderMeuOrcamentoUseCase(acesso, remoto).Executar(cliente.Id, ordem.Id, aprovado, aprovado ? null : "  Valor alto  ", default);
        Assert.Equal(aprovado ? "Aprovado" : "Recusado", output!.Status);
        Assert.Equal(sagas.Sagas[0].OrcamentoId, remoto.Recebido!.OrcamentoId); Assert.Equal(sagas.Sagas[0].PedidoOrcamentoId, remoto.Recebido.PedidoGeracaoId);
        Assert.Equal(status, sagas.Sagas[0].Status); Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordem.Status);
    }
    [Theory]
    [InlineData("os")]
    [InlineData("orcamento")]
    [InlineData("valor")]
    [InlineData("item")]
    [InlineData("status")]
    [InlineData("data")]
    public async Task RespostaRemotaDivergente_NaoRetornaOrcamentoAoCliente(string erro)
    {
        var (cliente, ordem, acesso, remoto, _, _) = Criar();
        remoto.Alterar = output =>
        {
            if (erro == "os") output.OrdemServicoId = Guid.NewGuid(); if (erro == "orcamento") output.Id = Guid.NewGuid();
            if (erro == "valor") output.ValorTotal = 1m; if (erro == "item") output.Itens[0].Total = 1m;
            if (erro == "status") output.Status = "Pago"; if (erro == "data") output.CriadoEm = default;
        };
        await Assert.ThrowsAsync<IntegracaoOrcamentoException>(() => new ObterMeuOrcamentoUseCase(acesso, remoto).Executar(cliente.Id, ordem.Id, default));
    }
    [Fact]
    public async Task RecusaSemMotivoOuAprovacaoComMotivo_NaoChamaCobranca()
    {
        var (cliente, ordem, acesso, remoto, _, _) = Criar(); var useCase = new ResponderMeuOrcamentoUseCase(acesso, remoto);
        await Assert.ThrowsAsync<ValidacaoException>(() => useCase.Executar(cliente.Id, ordem.Id, false, "\n", default));
        await Assert.ThrowsAsync<ValidacaoException>(() => useCase.Executar(cliente.Id, ordem.Id, true, "Motivo", default)); Assert.Equal(0, remoto.Chamadas);
    }
    private static (Cliente, OrdemServico, AcessoOrcamentoCliente, OrcamentoClienteGatewayFake, ClienteGatewayFake, SagaConsultaGatewayFake) Criar()
    {
        var clientes = new ClienteGatewayFake(); var cliente = new Cliente { Id = Guid.NewGuid() }; clientes.Clientes.Add(cliente);
        var ordens = new OrdemServicoGatewayFake(); var ordem = new OrdemServico { Id = Guid.NewGuid(), ClienteId = cliente.Id, Status = StatusOrdemServico.AguardandoAprovacao }; ordens.Ordens.Add(ordem);
        var sagas = new SagaConsultaGatewayFake(); sagas.Sagas.Add(new SagaAbertura
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordem.Id,
            OrcamentoId = Guid.NewGuid(),
            ExecucaoId = Guid.NewGuid(),
            PedidoOrcamentoId = Guid.NewGuid(),
            ValorOrcamento = 200m,
            CorrelationId = "teste-orcamento",
            Status = StatusSagaAbertura.OrcamentoGerado
        });
        return (cliente, ordem, new(clientes, ordens, sagas), new(), clientes, sagas);
    }
}
