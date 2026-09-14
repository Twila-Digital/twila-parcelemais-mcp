using System.Text;
using System.Text.Json;

namespace Parcelemais.Mcp.Http.Auth;

/// <summary>
/// Extrai a credencial do Parcele+ do header <c>Authorization: Bearer</c> ou <c>X-API-Key</c> e
/// disponibiliza em <see cref="HttpContext.Items"/> pro <c>SessionClientAccessor</c>. O "token" é o
/// próprio JSON de credencial (base64), exatamente como o access_token emitido em POST /token.
/// </summary>
public sealed class BearerCredentialMiddleware(RequestDelegate next)
{
    public const string ItemKey = "ParcelemaisCredential";

    public async Task InvokeAsync(HttpContext context)
    {
        var raw = ExtractToken(context.Request);

        if (raw is null || !TryDecode(raw, out var credential))
        {
            var proto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? context.Request.Scheme;
            var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? context.Request.Host.Value;
            var resourceMetadataUrl = $"{proto}://{host}/.well-known/oauth-protected-resource";

            context.Response.Headers.WWWAuthenticate = $"Bearer realm=\"parcelemais-mcp\", resource_metadata=\"{resourceMetadataUrl}\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                jsonrpc = "2.0",
                error = new { code = -32001, message = "Unauthorized: connect via OAuth or pass Authorization: Bearer <token>." },
                id = (object?)null,
            });
            return;
        }

        context.Items[ItemKey] = credential;
        await next(context);
    }

    private static string? ExtractToken(HttpRequest request)
    {
        var auth = request.Headers.Authorization.FirstOrDefault();
        if (auth is not null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth["Bearer ".Length..].Trim();
        }

        var apiKey = request.Headers["X-API-Key"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
    }

    private static bool TryDecode(string raw, out ParcelemaisCredential credential)
    {
        credential = null!;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            var decoded = JsonSerializer.Deserialize<ParcelemaisCredential>(json);
            if (decoded is null || string.IsNullOrEmpty(decoded.ClientId) || string.IsNullOrEmpty(decoded.ClientSecret))
            {
                return false;
            }

            credential = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
