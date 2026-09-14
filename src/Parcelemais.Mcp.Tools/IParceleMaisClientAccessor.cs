using ParceleMais;

namespace Parcelemais.Mcp.Tools;

/// <summary>
/// Resolve o <see cref="IParceleMaisClient"/> a usar na chamada MCP atual. O host stdio resolve um
/// client único configurado via variáveis de ambiente; o host HTTP resolve por credencial (a
/// requisição autenticada via OAuth aponta pra um ClientId/ClientSecret específico do Parcele+).
/// </summary>
public interface IParceleMaisClientAccessor
{
    Task<IParceleMaisClient> GetClientAsync(CancellationToken cancellationToken);
}
