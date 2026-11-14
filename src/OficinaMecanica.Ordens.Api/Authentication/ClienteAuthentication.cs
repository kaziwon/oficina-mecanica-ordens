using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Ordens.Infrastructure.Settings;

namespace OficinaMecanica.Ordens.Authentication;

public static class ClienteAuthentication
{
    public const string Scheme = "Cliente";
    public const string LoginRateLimit = "login-cliente";

    public static void Adicionar(IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(Scheme);
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<IOptions<JwtSettings>>((options, configured) =>
        {
            var jwt = configured.Value;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.ClienteAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "name",
                RoleClaimType = "role"
            };
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellation) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(new
                { message = "Muitas tentativas. Aguarde para tentar novamente.", correlationId = context.HttpContext.TraceIdentifier }, cancellation);
            };
        });
    }
}
