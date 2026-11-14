using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.Infrastructure.Sources;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.IntegrationTests;

public class OrcamentoClienteApiFactory(OrcamentoRemotoHandler handler) : OrdensApiFactory
{
    private readonly HttpClient _http = new(handler) { BaseAddress = new("http://cobranca/") };
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["OrcamentoCliente:Enabled"] = "true", ["OrcamentoCliente:BaseUrl"] = "http://cobranca/", ["OrcamentoCliente:SecretKey"] = OrcamentoRemotoHandler.Secret }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrcamentoClienteSource>();
            services.AddScoped<IOrcamentoClienteSource>(provider => new HttpOrcamentoClienteSource(_http, provider.GetRequiredService<IOptions<OrcamentoClienteSettings>>()));
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _http.Dispose(); }
}
