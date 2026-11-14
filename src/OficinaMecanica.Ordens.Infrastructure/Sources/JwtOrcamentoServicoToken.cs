using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public static class JwtOrcamentoServicoToken
{
    public static string Gerar(string secret)
    {
        var agora = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "OficinaMecanica.Ordens.Integracao",
            Audience = "OficinaMecanica.Cobranca.Orcamentos",
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.AddMinutes(2),
            Subject = new ClaimsIdentity([new Claim("sub", "ordens"), new Claim("role", "OrcamentosCliente"), new Claim("jti", Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256)
        });
        return handler.WriteToken(token);
    }
}
