using System.Text.Json;
using System.Text.Json.Serialization;
using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.Infrastructure.Messaging;

public static class MensagemSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    public static string Serializar(MensagemIntegracao mensagem)
    {
        var json = JsonSerializer.SerializeToNode(mensagem, Options)!;
        if (mensagem.CausationId is null) json["causationId"] = null;
        return json.ToJsonString();
    }

    public static MensagemIntegracao Desserializar(string body)
    {
        if (body.Length > 64000) throw new JsonException("Mensagem muito grande.");
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Envelope deve ser um objeto.");
        foreach (var campo in new[] { "messageId", "type", "version", "sagaId", "causationId", "occurredAt", "correlationId", "payload" })
            if (!document.RootElement.TryGetProperty(campo, out _)) throw new JsonException("Envelope incompleto.");
        return JsonSerializer.Deserialize<MensagemIntegracao>(body, Options)
            ?? throw new JsonException("Mensagem vazia.");
    }
}
