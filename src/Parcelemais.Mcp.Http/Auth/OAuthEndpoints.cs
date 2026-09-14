using System.Net.Http.Json;
using System.Text.Json;
using Parcelemais.Mcp.Http.Storage;

namespace Parcelemais.Mcp.Http.Auth;

/// <summary>
/// Authorization Server OAuth 2.0 (Dynamic Client Registration + Authorization Code + PKCE) —
/// necessário porque o conector remoto do Claude.ai exige OAuth, não aceita só um header estático.
/// O "access_token" emitido é, na prática, a própria credencial do Parcele+ (ClientId+ClientSecret
/// + ambiente), do mesmo jeito que a AbacatePay usa a própria API key como token.
/// </summary>
public static class OAuthEndpoints
{
    private static readonly HttpClient ValidationHttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static void MapOAuthEndpoints(this WebApplication app, DynamoDbOAuthStore store, Crypto crypto)
    {
        app.MapGet("/.well-known/oauth-protected-resource", (HttpRequest request) =>
        {
            var baseUrl = ServerBase(request);
            return Results.Json(new
            {
                resource = $"{baseUrl}/mcp",
                authorization_servers = new[] { baseUrl },
                bearer_methods_supported = new[] { "header" },
                resource_documentation = "https://github.com/Twila-Digital/twila-parcelemais-mcp",
                scopes_supported = new[] { "mcp" },
            });
        });

        app.MapGet("/.well-known/oauth-authorization-server", (HttpRequest request) =>
        {
            var baseUrl = ServerBase(request);
            return Results.Json(new
            {
                issuer = baseUrl,
                authorization_endpoint = $"{baseUrl}/authorize",
                token_endpoint = $"{baseUrl}/token",
                registration_endpoint = $"{baseUrl}/register",
                response_types_supported = new[] { "code" },
                grant_types_supported = new[] { "authorization_code" },
                code_challenge_methods_supported = new[] { "S256" },
                token_endpoint_auth_methods_supported = new[] { "none" },
                scopes_supported = new[] { "mcp" },
                revocation_endpoint = $"{baseUrl}/revoke",
            });
        });

        app.MapPost("/register", async (HttpRequest request, CancellationToken cancellationToken) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, cancellationToken: cancellationToken);
            if (!body.TryGetProperty("redirect_uris", out var redirectUrisEl) || redirectUrisEl.ValueKind != JsonValueKind.Array || redirectUrisEl.GetArrayLength() == 0)
            {
                return Results.Json(new { error = "invalid_client_metadata", error_description = "redirect_uris is required" }, statusCode: 400);
            }

            var redirectUris = redirectUrisEl.EnumerateArray().Select(e => e.GetString()!).ToArray();
            var clientName = body.TryGetProperty("client_name", out var nameEl) ? nameEl.GetString() : null;

            var client = await store.RegisterClientAsync(redirectUris, clientName, cancellationToken);

            return Results.Json(new
            {
                client_id = client.ClientId,
                client_secret = client.ClientSecret,
                redirect_uris = client.RedirectUris,
                client_name = client.ClientName,
                grant_types = new[] { "authorization_code" },
                response_types = new[] { "code" },
                token_endpoint_auth_method = "none",
            }, statusCode: 201);
        });

        app.MapGet("/authorize", async (HttpRequest request, DynamoDbOAuthStore oauthStore, CancellationToken cancellationToken) =>
        {
            var q = request.Query;
            var clientId = q["client_id"].ToString();
            var redirectUri = q["redirect_uri"].ToString();
            var codeChallenge = q["code_challenge"].ToString();
            var codeChallengeMethod = q["code_challenge_method"].ToString();
            var responseType = q["response_type"].ToString();
            var state = q["state"].ToString() is { Length: > 0 } s ? s : null;
            var scope = q["scope"].ToString() is { Length: > 0 } sc ? sc : null;
            var resource = q["resource"].ToString() is { Length: > 0 } r ? r : null;

            var validationError = await ValidateAuthorizeParamsAsync(clientId, redirectUri, codeChallenge, codeChallengeMethod, responseType, oauthStore, cancellationToken);
            if (validationError is not null)
            {
                return Results.Content(AuthorizePageHtml.Error(validationError), "text/html", statusCode: 400);
            }

            return Results.Content(AuthorizePageHtml.Render(clientId, redirectUri, codeChallenge, state, scope, resource, error: null, selectedEnvironment: "Production"), "text/html");
        });

        app.MapPost("/authorize", async (HttpRequest request, DynamoDbOAuthStore oauthStore, Crypto crypto, CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var clientId = form["client_id"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var codeChallenge = form["code_challenge"].ToString();
            var state = form["state"].ToString() is { Length: > 0 } s ? s : null;
            var scope = form["scope"].ToString() is { Length: > 0 } sc ? sc : null;
            var resource = form["resource"].ToString() is { Length: > 0 } r ? r : null;
            var environment = form["environment"].ToString() is "Staging" ? "Staging" : "Production";
            var parcelemaisClientId = form["parcelemais_client_id"].ToString();
            var parcelemaisClientSecret = form["parcelemais_client_secret"].ToString();

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeChallenge)
                || string.IsNullOrEmpty(parcelemaisClientId) || string.IsNullOrEmpty(parcelemaisClientSecret))
            {
                return Results.Content(AuthorizePageHtml.Error("Campos obrigatórios ausentes."), "text/html", statusCode: 400);
            }

            var registeredClient = await oauthStore.GetClientAsync(clientId, cancellationToken);
            if (registeredClient is null || !registeredClient.RedirectUris.Contains(redirectUri))
            {
                return Results.Content(AuthorizePageHtml.Error("Client ou redirect_uri inválido."), "text/html", statusCode: 400);
            }

            var valid = await ValidateParcelemaisCredentialAsync(environment, parcelemaisClientId, parcelemaisClientSecret, cancellationToken);
            if (!valid)
            {
                return Results.Content(
                    AuthorizePageHtml.Render(clientId, redirectUri, codeChallenge, state, scope, resource,
                        error: "Client ID ou Client Secret inválidos. Confira suas credenciais do Parcele+.",
                        selectedEnvironment: environment),
                    "text/html");
            }

            var credentialJson = JsonSerializer.Serialize(new ParcelemaisCredential(environment, parcelemaisClientId, parcelemaisClientSecret));
            var credentialEnc = crypto.Encrypt(credentialJson);

            var code = await oauthStore.CreateAuthCodeAsync(clientId, redirectUri, codeChallenge, credentialEnc, scope, resource, cancellationToken);

            var baseUrl = ServerBase(request);
            var redirectUrl = new UriBuilder(redirectUri);
            var queryParams = System.Web.HttpUtility.ParseQueryString(redirectUrl.Query);
            queryParams["code"] = code;
            queryParams["iss"] = baseUrl;
            if (state is not null) queryParams["state"] = state;
            redirectUrl.Query = queryParams.ToString();

            return Results.Redirect(redirectUrl.ToString());
        });

        app.MapPost("/token", async (HttpRequest request, DynamoDbOAuthStore oauthStore, Crypto crypto, CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var grantType = form["grant_type"].ToString();
            var code = form["code"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var codeVerifier = form["code_verifier"].ToString();
            var clientId = form["client_id"].ToString();
            var resource = form["resource"].ToString() is { Length: > 0 } r ? r : null;

            if (grantType != "authorization_code")
            {
                return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(clientId))
            {
                return Results.Json(new { error = "invalid_request", error_description = "Missing required parameters" }, statusCode: 400);
            }

            var entry = await oauthStore.ConsumeAuthCodeAsync(code, cancellationToken);
            if (entry is null)
            {
                return Results.Json(new { error = "invalid_grant", error_description = "Authorization code is invalid or expired" }, statusCode: 400);
            }

            if (entry.ClientId != clientId || entry.RedirectUri != redirectUri)
            {
                return Results.Json(new { error = "invalid_grant", error_description = "client_id or redirect_uri mismatch" }, statusCode: 400);
            }

            if (entry.Resource is not null && resource is not null && entry.Resource != resource)
            {
                return Results.Json(new { error = "invalid_target", error_description = "resource parameter does not match the authorized resource" }, statusCode: 400);
            }

            if (!PkceHelper.Verify(codeVerifier, entry.CodeChallenge))
            {
                return Results.Json(new { error = "invalid_grant", error_description = "PKCE verification failed" }, statusCode: 400);
            }

            var credentialJson = crypto.Decrypt(entry.CredentialEnc);
            var accessToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentialJson));

            return Results.Json(entry.Scope is null
                ? new { access_token = accessToken, token_type = "bearer" }
                : new { access_token = accessToken, token_type = "bearer", scope = entry.Scope });
        });

        app.MapPost("/revoke", () => Results.Ok());
    }

    private static async Task<string?> ValidateAuthorizeParamsAsync(
        string clientId, string redirectUri, string codeChallenge, string codeChallengeMethod, string responseType,
        DynamoDbOAuthStore store, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(clientId)) return "Missing client_id.";
        if (string.IsNullOrEmpty(redirectUri)) return "Missing redirect_uri.";
        if (string.IsNullOrEmpty(codeChallenge)) return "Missing code_challenge.";
        if (codeChallengeMethod != "S256") return "Only S256 code_challenge_method is supported.";
        if (responseType != "code") return "Only response_type=code is supported.";
        if (await store.GetClientAsync(clientId, cancellationToken) is null) return "Unknown client_id. Please reconnect the MCP client.";
        return null;
    }

    private static async Task<bool> ValidateParcelemaisCredentialAsync(string environment, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var baseUrl = environment == "Staging"
            ? "https://api.staging.parcelemais.com.br/integration/"
            : "https://api.parcelemais.com.br/integration/";

        try
        {
            using var response = await ValidationHttpClient.PostAsJsonAsync(
                $"{baseUrl}v1/authentication/accesstoken",
                new { clientId, clientSecret },
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string ServerBase(HttpRequest request)
    {
        var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        return $"{proto}://{host}";
    }
}
