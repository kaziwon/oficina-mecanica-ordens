using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class JwtClienteTokenSource(IOptions<JwtSettings> configured) : IClienteTokenSource
{
    public TokenGerado GerarToken(Guid clienteId, string nome)
    {
        var settings = configured.Value;
        var agora = DateTime.UtcNow;
        var expiraEm = agora.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.ClienteAudience,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, clienteId.ToString()),
                new Claim("name", nome), new Claim("role", "Cliente"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        return new TokenGerado { Token = handler.WriteToken(handler.CreateToken(descriptor)), ExpiresAt = expiraEm };
    }
}
