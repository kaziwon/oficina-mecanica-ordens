using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class OrcamentoRemotoHandler : HttpMessageHandler
{
    public const string Secret = "chave-exclusiva-de-integracao-ordens-cobranca-nos-testes-1234567890";
    public OrcamentoClienteOutput Orcamento { get; set; } = new();
    public string Modo { get; set; } = string.Empty;
    public int Chamadas { get; private set; }
    public int Decisoes { get; private set; }
    public string? UltimoToken { get; private set; }
    public JsonElement UltimoBody { get; private set; }
    private bool _falhou;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Chamadas++;
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme); UltimoToken = request.Headers.Authorization!.Parameter;
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(UltimoToken, new TokenValidationParameters
        {
            ValidIssuer = "OficinaMecanica.Ordens.Integracao",
            ValidAudience = "OficinaMecanica.Cobranca.Orcamentos",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        }, out _);
        Assert.Equal("ordens", principal.FindFirst("sub")!.Value); Assert.Equal("OrcamentosCliente", principal.FindFirst("role")!.Value);
        Assert.True(request.Headers.Contains("X-Correlation-ID"));
        if (Modo == "indisponivel") return new(HttpStatusCode.ServiceUnavailable);
        if (Modo == "json") return new(HttpStatusCode.OK) { Content = new StringContent("nao e json") };
        if (request.Method == HttpMethod.Post)
        {
            UltimoBody = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var aprovado = request.RequestUri!.AbsolutePath.EndsWith("/aprovar", StringComparison.Ordinal);
            var motivo = UltimoBody.GetProperty("motivo").GetString(); var esperado = aprovado ? "Aprovado" : "Recusado";
            if (Orcamento.Status != "AguardandoAprovacao" && (Orcamento.Status != esperado || Orcamento.MotivoRecusa != motivo)) return new(HttpStatusCode.Conflict);
            if (Orcamento.Status == "AguardandoAprovacao")
            { Decisoes++; Orcamento.Status = esperado; Orcamento.RespondidoEm = DateTime.UtcNow; Orcamento.MotivoRecusa = motivo; }
            if (Modo == "timeout-depois-gravar" && !_falhou) { _falhou = true; throw new HttpRequestException("Conexao caiu depois da gravacao simulada"); }
            return new(HttpStatusCode.NoContent);
        }
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(Orcamento) };
    }
}
