using ApiInventario.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            BusinessRuleException business => (
                business.Error switch
                {
                    BusinessError.Conflict => StatusCodes.Status409Conflict,
                    BusinessError.NotFound => StatusCodes.Status404NotFound,
                    BusinessError.InvalidCredentials => StatusCodes.Status401Unauthorized,
                    BusinessError.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                }, "No se pudo completar la solicitud", business.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                "El producto cambió durante la operación. Consulta su estado actual y vuelve a intentarlo."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor",
                "Ocurrió un error inesperado. Vuelve a intentarlo más tarde.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error inesperado. TraceId: {TraceId}", httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = status;
        if (status == StatusCodes.Status401Unauthorized)
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

        var problem = new ProblemDetails
        {
            Status = status, Title = title, Detail = detail,
            Type = "about:blank", Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext, ProblemDetails = problem
            }))
        {
            // An unsupported Accept header must not turn error handling into a second exception.
            await httpContext.Response.WriteAsJsonAsync(
                problem, options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }
        return true;
    }
}
