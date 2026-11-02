using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.IntegrationTests;

internal static class ApiTestHelper
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task Autenticar(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "SenhaDosTestes@123"
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    public static object Cliente(string cpf = "52998224725") => new
    {
        nome = "Cliente Teste",
        cpfCnpj = cpf,
        email = "cliente@example.com",
        telefone = "11999999999"
    };

    public static object Veiculo() => new
    {
        placa = "ABC1D23",
        marca = "Fiat",
        modelo = "Uno",
        ano = 2020
    };

    public static object OrdemCompleta(string cpf = "52998224725") => new
    {
        cliente = Cliente(cpf),
        veiculo = Veiculo(),
        descricaoSolicitacao = "Revisao dos freios"
    };
}
