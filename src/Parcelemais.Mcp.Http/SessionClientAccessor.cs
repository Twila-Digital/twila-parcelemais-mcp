using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using Parcelemais.Mcp.Http.Auth;
using Parcelemais.Mcp.Tools;
using ParceleMais;
using ParceleMais.Configuration;
using ParceleMais.DependencyInjection;

namespace Parcelemais.Mcp.Http;

/// <summary>
/// Resolve o <see cref="IParceleMaisClient"/> da credencial autenticada na requisição HTTP atual
/// (ver <see cref="BearerCredentialMiddleware"/>). Clients são cacheados por credencial — reaproveita
/// o cache de token/circuit breaker do SDK entre chamadas do mesmo usuário, em vez de recriar a cada tool call.
/// </summary>
public sealed class SessionClientAccessor(IHttpContextAccessor httpContextAccessor) : IParceleMaisClientAccessor
{
    private static readonly ConcurrentDictionary<string, IParceleMaisClient> Cache = new();

    public Task<IParceleMaisClient> GetClientAsync(CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new McpException("Sem contexto HTTP — a requisição não passou pela autenticação.");

        if (context.Items[BearerCredentialMiddleware.ItemKey] is not ParcelemaisCredential credential)
        {
            throw new McpException("Credencial do Parcele+ não encontrada na requisição.");
        }

        var cacheKey = $"{credential.Environment}:{credential.ClientId}:{credential.ClientSecret}";

        var client = Cache.GetOrAdd(cacheKey, _ => BuildClient(credential));
        return Task.FromResult(client);
    }

    private static IParceleMaisClient BuildClient(ParcelemaisCredential credential)
    {
        var services = new ServiceCollection();
        services.AddParceleMais(options =>
        {
            options.ClientId = credential.ClientId;
            options.ClientSecret = credential.ClientSecret;
            options.Environment = credential.Environment == "Staging"
                ? ParceleMaisEnvironment.Staging
                : ParceleMaisEnvironment.Production;
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IParceleMaisClient>();
    }
}
