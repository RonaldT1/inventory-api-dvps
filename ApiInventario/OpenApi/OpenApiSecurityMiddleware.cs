using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiInventario.OpenApi;

internal sealed class OpenApiSecurityMiddleware
{
    private readonly RequestDelegate _next;

    public OpenApiSecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(memoryStream).ReadToEndAsync();

            try
            {
                if (context.Response.StatusCode is >= 200 and < 300)
                {
                    var doc = JsonNode.Parse(json);
                    if (doc is not null)
                    {
                        // Add document-level security requirement.
                        doc["security"] = new JsonArray(
                            new JsonObject { ["Bearer"] = new JsonArray() }
                        );

                        json = doc.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                    }
                }
            }
            catch (JsonException)
            {
                // Preserve responses that are not OpenAPI JSON documents.
            }

            context.Response.Body = originalBody;
            context.Response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(json);
            await context.Response.WriteAsync(json);
        }
        finally
        {
            // The global handler must write to the real response if downstream code throws.
            context.Response.Body = originalBody;
        }
    }
}
