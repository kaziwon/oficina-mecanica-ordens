using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Ordens.Infrastructure.Settings;

namespace OficinaMecanica.Ordens.IntegrationTests;

internal static class ClienteJwtTestHelper
{
    public static string Gerar(OrdensApiFactory factory, Guid clienteId, string erro)
    {
        var settings = factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var claims = new List<Claim> { new("sub", clienteId.ToString()), new("role", "Cliente") };
        if (erro == "sem-sub") claims.RemoveAll(item => item.Type == "sub");
        if (erro == "sub-invalido") claims[0] = new("sub", "nao-e-guid");
        if (erro == "sub-vazio") claims[0] = new("sub", Guid.Empty.ToString());
        if (erro == "sub-duplicado") claims.Add(new("sub", Guid.NewGuid().ToString()));
        if (erro == "role") claims[1] = new("role", "Admin");
        var assinatura = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            erro == "assinatura" ? "chave-incorreta-dos-testes-de-cliente-1234567890" : settings.SecretKey)), SecurityAlgorithms.HmacSha256);
        var agora = DateTime.UtcNow;
        var token = new JwtSecurityToken(erro == "issuer" ? "outro-emissor" : settings.Issuer,
            erro == "audience" ? settings.Audience : settings.ClienteAudience, claims,
            agora.AddHours(-1), erro == "expirado" ? agora.AddMinutes(-5) : agora.AddMinutes(30),
            erro == "sem-assinatura" ? null : assinatura);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
