using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Ordens.Application.Exceptions;

namespace OficinaMecanica.Ordens.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidacaoException exception)
        {
            await EscreverErro(context, 400, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Conflito ao processar a requisicao");
            await EscreverErro(context, 409, exception.Message);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Falha ao persistir dados da requisicao");
            await EscreverErro(context, 500, "Nao foi possivel persistir os dados.");
        }
    }

    private static Task EscreverErro(HttpContext context, int status, string mensagem)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { message = mensagem, correlationId = context.TraceIdentifier });
    }
}
