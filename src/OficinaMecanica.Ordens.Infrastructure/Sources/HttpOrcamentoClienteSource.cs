using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class HttpOrcamentoClienteSource(HttpClient http, IOptions<OrcamentoClienteSettings> configured) : IOrcamentoClienteSource
{
    public Task<OrcamentoClienteOutput> Obter(VinculoOrcamentoClienteInput vinculo, CancellationToken cancellation)
        => Executar(vinculo, null, null, cancellation);
    public Task<OrcamentoClienteOutput> Responder(VinculoOrcamentoClienteInput vinculo, bool aprovado, string? motivo, CancellationToken cancellation)
        => Executar(vinculo, aprovado, motivo, cancellation);

    private async Task<OrcamentoClienteOutput> Executar(VinculoOrcamentoClienteInput vinculo, bool? aprovado, string? motivo, CancellationToken cancellation)
    {
        var settings = configured.Value;
        if (!settings.Enabled) throw new IntegracaoOrcamentoException("Consulta de orcamento indisponivel neste ambiente.");
        try
        {
            var dados = new { vinculo.OrdemServicoId, vinculo.ExecucaoId, vinculo.SagaId, vinculo.PedidoGeracaoId, vinculo.ValorTotal, vinculo.CorrelationId };
            var rota = $"internal/orcamentos/{vinculo.OrcamentoId}";
            if (aprovado.HasValue)
            {
                using var post = Criar(HttpMethod.Post, rota + (aprovado.Value ? "/aprovar" : "/recusar"), vinculo);
                post.Content = JsonContent.Create(new { vinculo = dados, motivo });
                using var resposta = await http.SendAsync(post, cancellation);
                ValidarStatus(resposta);
                if (resposta.StatusCode != HttpStatusCode.NoContent) throw new IntegracaoOrcamentoException("Decisao sem confirmacao de Cobranca.");
            }
            // Uma nova requisicao le o estado persistido, inclusive apos repeticao de uma decisao.
            using var query = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ordemServicoId"] = vinculo.OrdemServicoId.ToString(),
                ["execucaoId"] = vinculo.ExecucaoId.ToString(),
                ["sagaId"] = vinculo.SagaId.ToString(),
                ["pedidoGeracaoId"] = vinculo.PedidoGeracaoId.ToString(),
                ["valorTotal"] = vinculo.ValorTotal.ToString(CultureInfo.InvariantCulture),
                ["correlationId"] = vinculo.CorrelationId
            });
            using var get = Criar(HttpMethod.Get, rota + "?" + await query.ReadAsStringAsync(cancellation), vinculo);
            using var response = await http.SendAsync(get, cancellation);
            ValidarStatus(response);
            if (response.StatusCode != HttpStatusCode.OK) throw new IntegracaoOrcamentoException("Consulta de orcamento nao confirmada.");
            return await response.Content.ReadFromJsonAsync<OrcamentoClienteOutput>(cancellation)
                ?? throw new IntegracaoOrcamentoException("Resposta vazia de Cobranca.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException or IOException)
        { throw new IntegracaoOrcamentoException("Cobranca indisponivel ou resultado nao confirmado. Consulte o orcamento antes de repetir.", exception); }
    }
    private HttpRequestMessage Criar(HttpMethod method, string rota, VinculoOrcamentoClienteInput vinculo)
    {
        var request = new HttpRequestMessage(method, rota);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtOrcamentoServicoToken.Gerar(configured.Value.SecretKey));
        request.Headers.Add("X-Correlation-ID", vinculo.CorrelationId);
        return request;
    }
    private static void ValidarStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Conflict) throw new InvalidOperationException("O orcamento ja recebeu outra resposta ou seu vinculo foi alterado. Consulte o estado atual.");
        if (!response.IsSuccessStatusCode) throw new IntegracaoOrcamentoException("Nao foi possivel confirmar o orcamento em Cobranca.");
    }
}
