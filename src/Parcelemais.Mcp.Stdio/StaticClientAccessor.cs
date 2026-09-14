using Parcelemais.Mcp.Tools;
using ParceleMais;

namespace Parcelemais.Mcp.Stdio;

/// <summary>Um único <see cref="IParceleMaisClient"/>, configurado uma vez via variáveis de ambiente no start do processo.</summary>
internal sealed class StaticClientAccessor(IParceleMaisClient client) : IParceleMaisClientAccessor
{
    public Task<IParceleMaisClient> GetClientAsync(CancellationToken cancellationToken) => Task.FromResult(client);
}
