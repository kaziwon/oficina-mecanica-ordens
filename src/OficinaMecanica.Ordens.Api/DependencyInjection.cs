using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.UseCases.Auth;
using OficinaMecanica.Ordens.Application.UseCases.Clientes;
using OficinaMecanica.Ordens.Application.UseCases.Veiculos;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;
using OficinaMecanica.Ordens.Infrastructure.Persistence;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.Controllers;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;
using OficinaMecanica.Ordens.InterfaceAdapters.Gateways;

namespace OficinaMecanica.Ordens;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdens(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Infrastructure.Settings.MessagingSettings>().Bind(configuration.GetSection("Messaging"));
        services.AddSingleton<Infrastructure.Messaging.SqsTransport>();
        services.AddHostedService<Messaging.IntegracaoWorker>();
        services.AddScoped<IOutboxDataSource, EfOutboxDataSource>();
        services.AddScoped<IOutboxGateway, OutboxGateway>();
        services.AddScoped<ISagaAberturaDataSource, EfSagaAberturaDataSource>();
        services.AddScoped<ISagaAberturaGateway, SagaAberturaGateway>();
        services.AddScoped<ObterSagaAberturaUseCase>();
        services.AddScoped<CancelarEntradaExecucaoUseCase>();
        services.AddScoped<ProcessarRespostaAberturaUseCase>();
        services.AddScoped<ProcessarEventoDiagnosticoUseCase>();
        services.AddScoped<ProcessarRespostaOrcamentoUseCase>();
        services.AddScoped<IntegracaoCleanController>();
        services.AddScoped<SagaAberturaCleanController>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0)));
        });

        services.AddOptions<JwtSettings>().Bind(configuration.GetSection("JwtSettings"))
            .Validate(settings => Encoding.UTF8.GetByteCount(settings.SecretKey) >= 32,
                "Configure JwtSettings:SecretKey com pelo menos 32 bytes.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer)
                && !string.IsNullOrWhiteSpace(settings.Audience), "Configure issuer e audience do JWT.")
            .ValidateOnStart();
        services.AddOptions<AdminUserSettings>().Bind(configuration.GetSection("AdminUserSettings"))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Username)
                && !string.IsNullOrWhiteSpace(settings.Password), "Configure as credenciais administrativas.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, configured) =>
            {
                var jwt = configured.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();

        services.AddScoped<IClienteDataSource, EfClienteDataSource>();
        services.AddScoped<IVeiculoDataSource, EfVeiculoDataSource>();
        services.AddScoped<IOrdemServicoDataSource, EfOrdemServicoDataSource>();
        services.AddScoped<IAdminUserSource, AdminUserSettingsSource>();
        services.AddScoped<ITokenSource, JwtTokenSource>();
        services.AddScoped<ICpfCnpjValidatorSource, CpfCnpjValidatorSource>();
        services.AddScoped<IPlacaVeiculoValidatorSource, BrazilianDocumentsPlacaVeiculoValidatorSource>();
        services.AddScoped<IClienteGateway, ClienteGateway>();
        services.AddScoped<IVeiculoGateway, VeiculoGateway>();
        services.AddScoped<IOrdemServicoGateway, OrdemServicoGateway>();
        services.AddScoped<IAdminUserGateway, AdminUserGateway>();
        services.AddScoped<ITokenGateway, TokenGateway>();
        services.AddScoped<ICpfCnpjValidatorGateway, CpfCnpjValidatorGateway>();
        services.AddScoped<IPlacaVeiculoValidatorGateway, PlacaVeiculoValidatorGateway>();

        services.AddScoped<LoginUseCase>();
        services.AddScoped<ObterTodosClientesUseCase>();
        services.AddScoped<ObterClientePorIdUseCase>();
        services.AddScoped<CriarClienteUseCase>();
        services.AddScoped<AtualizarClienteUseCase>();
        services.AddScoped<RemoverClienteUseCase>();
        services.AddScoped<ObterTodosVeiculosUseCase>();
        services.AddScoped<ObterVeiculoPorIdUseCase>();
        services.AddScoped<CriarVeiculoUseCase>();
        services.AddScoped<AtualizarVeiculoUseCase>();
        services.AddScoped<RemoverVeiculoUseCase>();
        services.AddScoped<CriarOrdemServicoUseCase>();
        services.AddScoped<ObterOrdemServicoPorIdUseCase>();
        services.AddScoped<ObterTodasOrdensServicoUseCase>();
        services.AddScoped<AuthCleanController>();
        services.AddScoped<ClientesCleanController>();
        services.AddScoped<VeiculosCleanController>();
        services.AddScoped<OrdensServicoCleanController>();
        return services;
    }
}
