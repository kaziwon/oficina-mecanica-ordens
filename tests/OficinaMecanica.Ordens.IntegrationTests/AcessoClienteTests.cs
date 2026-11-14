using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class AcessoClienteTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public async Task Login_TokenClienteComAudiencePropriaSemCpf(string cpf)
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var ordem = await CriarOrdem(client); var response = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf });
        response.EnsureSuccessStatusCode(); var login = (await response.Content.ReadFromJsonAsync<LoginClienteResponseDto>())!;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        Assert.Equal(ordem.ClienteId, login.ClienteId); Assert.Equal(ordem.ClienteId.ToString(), token.Subject);
        Assert.Equal("Cliente", token.Claims.Single(item => item.Type == "role").Value);
        Assert.Equal(factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value.ClienteAudience, Assert.Single(token.Audiences));
        Assert.DoesNotContain(token.Claims, item => item.Type.Contains("cpf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("52998224725", token.Payload.SerializeToJson()); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.InRange(login.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000000000")]
    [InlineData("52998224724")]
    [InlineData("abc52998224725")]
    [InlineData("529 982 247 25")]
    [InlineData("11222333000181")]
    public async Task CpfInvalido_Retorna400(string? cpf)
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/cliente", new { cpf })).StatusCode);
    }

    [Fact]
    public async Task AusenteEInativo_MesmaRespostaSemEmitirToken()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var ausente = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725" }); var ordem = await CriarOrdem(client);
        (await client.DeleteAsync($"/api/clientes/{ordem.ClienteId}")).EnsureSuccessStatusCode();
        var inativo = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725" });
        Assert.Equal(HttpStatusCode.Unauthorized, ausente.StatusCode); Assert.Equal(ausente.StatusCode, inativo.StatusCode);
        var problemaAusente = await ausente.Content.ReadFromJsonAsync<JsonElement>();
        var problemaInativo = await inativo.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var campo in new[] { "type", "title", "status" })
            Assert.Equal(problemaAusente.GetProperty(campo).ToString(), problemaInativo.GetProperty(campo).ToString());
        Assert.False(problemaAusente.TryGetProperty("token", out _)); Assert.False(problemaInativo.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Consultas_SomentePropriasInclusiveEncerradasSemAceitarClienteIdDaQuery()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var propria = await CriarOrdem(client); var alheia = await CriarOrdem(client, "11144477735", "DEF2G34");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var status in new[] { StatusOrdemServico.Finalizada, StatusOrdemServico.Entregue, StatusOrdemServico.Cancelada })
                db.OrdensServico.Add(new OrdemServico
                {
                    Id = Guid.NewGuid(),
                    ClienteId = propria.ClienteId,
                    VeiculoId = propria.VeiculoId,
                    Status = status,
                    CriadaEm = propria.CriadaEm.AddMinutes(1),
                    DescricaoSolicitacao = "Historico do cliente"
                });
            db.SaveChanges();
        }
        var login = await Login(client); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        var lista = (await client.GetFromJsonAsync<List<OrdemServicoResponseDto>>($"/api/minhas-ordens?clienteId={alheia.ClienteId}&cpf=11144477735", ApiTestHelper.Json))!;
        Assert.Equal(4, lista.Count); Assert.All(lista, item => Assert.Equal(propria.ClienteId, item.ClienteId));
        Assert.DoesNotContain(lista, item => item.Id == alheia.Id); Assert.Equal(propria.Id, lista[^1].Id);
        var consulta = await client.GetFromJsonAsync<OrdemServicoResponseDto>($"/api/minhas-ordens/{propria.Id}", ApiTestHelper.Json);
        Assert.Equal(propria.Id, consulta!.Id);
        var outra = await client.GetAsync($"/api/minhas-ordens/{alheia.Id}"); var ausente = await client.GetAsync($"/api/minhas-ordens/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, outra.StatusCode); Assert.Equal(outra.StatusCode, ausente.StatusCode);
    }

    [Theory]
    [InlineData("/api/clientes", "GET")]
    [InlineData("/api/veiculos", "GET")]
    [InlineData("/api/ordensservico", "GET")]
    [InlineData("/api/ordensservico", "POST")]
    public async Task TokenCliente_NuncaAutorizaRotasAdministrativas(string rota, string metodo)
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client); await CriarOrdem(client);
        var login = await Login(client); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task SemTokenOuComTokenAdmin_NaoUsaRotasExclusivasDeCliente()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/minhas-ordens")).StatusCode);
        await ApiTestHelper.Autenticar(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/ordensservico")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/minhas-ordens")).StatusCode);
    }

    [Theory]
    [InlineData("assinatura", HttpStatusCode.Unauthorized)]
    [InlineData("expirado", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("issuer", HttpStatusCode.Unauthorized)]
    [InlineData("sem-assinatura", HttpStatusCode.Unauthorized)]
    [InlineData("sem-sub", HttpStatusCode.Unauthorized)]
    [InlineData("sub-invalido", HttpStatusCode.Unauthorized)]
    [InlineData("sub-vazio", HttpStatusCode.Unauthorized)]
    [InlineData("sub-duplicado", HttpStatusCode.Unauthorized)]
    [InlineData("role", HttpStatusCode.Forbidden)]
    public async Task TokenInvalidoOuSemPermissao_NaoConsulta(string erro, HttpStatusCode esperado)
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient(); await ApiTestHelper.Autenticar(client);
        var ordem = await CriarOrdem(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ClienteJwtTestHelper.Gerar(factory, ordem.ClienteId, erro));
        Assert.Equal(esperado, (await client.GetAsync("/api/minhas-ordens")).StatusCode);
    }

    [Fact]
    public async Task Desativacao_RevogaConsultasSemEsperarExpiracaoDoToken()
    {
        using var factory = new OrdensApiFactory(); using var admin = factory.CreateClient(); await ApiTestHelper.Autenticar(admin);
        var ordem = await CriarOrdem(admin); var login = await Login(admin); using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        (await client.GetAsync("/api/minhas-ordens")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/clientes/{ordem.ClienteId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/minhas-ordens")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/minhas-ordens/{ordem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725" })).StatusCode);
    }

    [Fact]
    public async Task Login_NaoAceitaPapelOuClienteIdNoBody()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725", role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725", clienteId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Login_LimitaTentativasSemBloquearLoginAdministrativo()
    {
        using var factory = new OrdensApiFactory(); using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "invalido" })).StatusCode);
        var limitada = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "invalido" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limitada.StatusCode); Assert.NotNull(limitada.Headers.RetryAfter);
        await ApiTestHelper.Autenticar(client);
    }

    private static async Task<OrdemServicoResponseDto> CriarOrdem(HttpClient client, string cpf = "52998224725", string placa = "ABC1D23")
    {
        var response = await client.PostAsJsonAsync("/api/ordensservico", new
        { cliente = ApiTestHelper.Cliente(cpf), veiculo = new { placa, marca = "Fiat", modelo = "Uno", ano = 2020 }, descricaoSolicitacao = "Revisao" });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<OrdemServicoResponseDto>(ApiTestHelper.Json))!;
    }
    private static async Task<LoginClienteResponseDto> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/cliente", new { cpf = "52998224725" }); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginClienteResponseDto>())!;
    }
}
