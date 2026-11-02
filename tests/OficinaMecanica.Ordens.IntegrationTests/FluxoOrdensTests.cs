using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class FluxoOrdensTests
{
    [Fact]
    public async Task Health_E_Swagger_SaoPublicos()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "teste-fase4-001");

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("teste-fase4-001", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Theory]
    [InlineData("/api/clientes")]
    [InlineData("/api/veiculos")]
    [InlineData("/api/ordensservico")]
    public async Task RotasProtegidas_ExigemToken(string rota)
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(rota)).StatusCode);
    }

    [Fact]
    public async Task Login_RejeitaSenhaIncorreta()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fluxo_ComCadastrosExistentes_CriaEConsultaOrdemComHistorico()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);

        var clienteResponse = await client.PostAsJsonAsync("/api/clientes", ApiTestHelper.Cliente());
        Assert.Equal(HttpStatusCode.Created, clienteResponse.StatusCode);
        var cliente = (await clienteResponse.Content.ReadFromJsonAsync<ClienteResponseDto>())!;
        var veiculoResponse = await client.PostAsJsonAsync("/api/veiculos", new
        {
            clienteId = cliente.Id,
            placa = "ABC1D23",
            marca = "Fiat",
            modelo = "Uno",
            ano = 2020
        });
        Assert.Equal(HttpStatusCode.Created, veiculoResponse.StatusCode);
        var veiculo = (await veiculoResponse.Content.ReadFromJsonAsync<VeiculoResponseDto>())!;
        var response = await client.PostAsJsonAsync("/api/ordensservico", new
        {
            cpfCnpj = cliente.CpfCnpj,
            veiculoId = veiculo.Id,
            descricaoSolicitacao = "Revisar os freios"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var ordem = (await response.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
        Assert.Equal(cliente.Id, ordem.ClienteId);
        Assert.Equal(veiculo.Id, ordem.VeiculoId);
        Assert.Equal(StatusOrdemServico.Recebida, ordem.Status);
        Assert.Equal(StatusOrdemServico.Recebida, Assert.Single(ordem.Historico).Status);
        var consultada = await client.GetFromJsonAsync<OrdemServicoResponseDto>(response.Headers.Location, ApiTestHelper.Json);
        Assert.Equal(ordem.Id, consultada!.Id);
        Assert.Single(consultada.Historico);
        Assert.Single((await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>("/api/ordensservico", ApiTestHelper.Json))!);
    }

    [Fact]
    public async Task Fluxo_ComCadastroCompleto_PersisteClienteVeiculoEOrdem()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);

        var response = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ordem = (await response.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/clientes/{ordem.ClienteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/veiculos/{ordem.VeiculoId}")).StatusCode);
    }

    [Fact]
    public async Task CadastroCompleto_PlacaInvalida_NaoDeixaClienteParcial()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var response = await client.PostAsJsonAsync("/api/ordensservico", new
        {
            cliente = ApiTestHelper.Cliente(),
            veiculo = new { placa = "INVALIDA", marca = "Fiat", modelo = "Uno", ano = 2020 },
            descricaoSolicitacao = "Revisao"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ClienteResponseDto>>("/api/clientes"))!);
    }

    [Fact]
    public async Task CadastroCompleto_ConflitoNoBanco_DesfazTodosOsInserts()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cliente = new Cliente { Id = Guid.NewGuid(), CpfCnpj = "39053344705", Nome = "Segundo" };
            var veiculo = new Veiculo { Id = Guid.NewGuid(), ClienteId = cliente.Id, Cliente = cliente, Placa = "ABC1D23" };
            var ordem = new OrdemServico
            {
                Id = Guid.NewGuid(),
                ClienteId = cliente.Id,
                Cliente = cliente,
                VeiculoId = veiculo.Id,
                Veiculo = veiculo,
                Status = StatusOrdemServico.Recebida,
                DescricaoSolicitacao = "Conflito de placa",
                CriadaEm = DateTime.UtcNow
            };
            Assert.Throws<DbUpdateException>(() => new EfOrdemServicoDataSource(context).Adicionar(ordem));
        }

        using var verificationScope = factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, database.Clientes.Count());
        Assert.Equal(1, database.Veiculos.Count());
        Assert.Equal(1, database.OrdensServico.Count());
    }

    [Fact]
    public async Task CadastroCompleto_RejeitaCpfDuplicado()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/clientes", ApiTestHelper.Cliente())).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Ordem_RejeitaVeiculoDeOutroCliente()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var response = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta());
        response.EnsureSuccessStatusCode();
        var ordem = (await response.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
        (await client.PostAsJsonAsync("/api/clientes", ApiTestHelper.Cliente("39053344705"))).EnsureSuccessStatusCode();
        var tentativa = await client.PostAsJsonAsync("/api/ordensservico", new
        {
            cpfCnpj = "39053344705",
            veiculoId = ordem.VeiculoId,
            descricaoSolicitacao = "Revisao"
        });
        Assert.Equal(HttpStatusCode.Conflict, tentativa.StatusCode);
    }

    [Fact]
    public async Task Ordem_RejeitaMisturaDasDuasOpcoes()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var response = await client.PostAsJsonAsync("/api/ordensservico", new
        {
            cpfCnpj = "52998224725",
            veiculoId = Guid.NewGuid(),
            cliente = ApiTestHelper.Cliente(),
            veiculo = ApiTestHelper.Veiculo(),
            descricaoSolicitacao = "Revisao"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ordensservico?ordenacao=invalido")]
    [InlineData("/api/ordensservico?status=99")]
    [InlineData("/api/ordensservico?status=invalido")]
    public async Task Listagem_RejeitaFiltrosInvalidos(string rota)
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(rota)).StatusCode);
    }

    [Fact]
    public async Task Ordem_RejeitaCamposDeOutrosServicosEmVezDeIgnorar()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var response = await client.PostAsJsonAsync("/api/ordensservico", new
        {
            cpfCnpj = "52998224725",
            veiculoId = Guid.NewGuid(),
            descricaoSolicitacao = "Revisao",
            valorPagamento = 200
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cadastro_AtualizaEDesativaSemApagarHistoricoDaOS()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        var created = await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta());
        created.EnsureSuccessStatusCode();
        var ordem = (await created.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/clientes/{ordem.ClienteId}", new
        {
            nome = "Nome Atualizado",
            cpfCnpj = "52998224725",
            email = "novo@example.com",
            telefone = "11888888888"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/veiculos/{ordem.VeiculoId}", new
        {
            clienteId = ordem.ClienteId,
            placa = "ABC1D23",
            marca = "Fiat",
            modelo = "Uno Atualizado",
            ano = 2021
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/veiculos/{ordem.VeiculoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/clientes/{ordem.ClienteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clientes/{ordem.ClienteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/veiculos/{ordem.VeiculoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/ordensservico/{ordem.Id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Single(scope.ServiceProvider.GetRequiredService<AppDbContext>().HistoricoStatus);
    }

    [Fact]
    public async Task Listagem_RespeitaPrioridadeDataEFiltroEExcluiEncerradas()
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        (await client.PostAsJsonAsync("/api/ordensservico", ApiTestHelper.OrdemCompleta())).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recebida = db.OrdensServico.Single();
            foreach (var status in Enum.GetValues<StatusOrdemServico>())
            {
                db.OrdensServico.Add(new OrdemServico
                {
                    Id = Guid.NewGuid(),
                    ClienteId = recebida.ClienteId,
                    VeiculoId = recebida.VeiculoId,
                    Status = status,
                    CriadaEm = recebida.CriadaEm.AddDays(-1),
                    DescricaoSolicitacao = "Ordem de teste"
                });
            }
            db.SaveChanges();
        }

        var lista = (await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>("/api/ordensservico", ApiTestHelper.Json))!;
        Assert.Equal(5, lista.Count);
        Assert.Equal(StatusOrdemServico.EmExecucao, lista[0].Status);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, lista[1].Status);
        Assert.Equal(StatusOrdemServico.EmDiagnostico, lista[2].Status);
        Assert.True(lista[3].CriadaEm < lista[4].CriadaEm);
        var filtrada = (await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>(
            "/api/ordensservico?status=Recebida&ordenacao=maisNovo", ApiTestHelper.Json))!;
        Assert.Equal(2, filtrada.Count);
        Assert.True(filtrada[0].CriadaEm > filtrada[1].CriadaEm);
    }

    [Theory]
    [InlineData("/api/clientes/")]
    [InlineData("/api/veiculos/")]
    [InlineData("/api/ordensservico/")]
    public async Task Consulta_IdInexistente_Retorna404(string prefixo)
    {
        using var factory = new OrdensApiFactory();
        using var client = factory.CreateClient();
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(prefixo + Guid.NewGuid())).StatusCode);
    }
}
