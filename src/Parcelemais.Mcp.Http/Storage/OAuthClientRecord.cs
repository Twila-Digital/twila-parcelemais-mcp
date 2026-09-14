namespace Parcelemais.Mcp.Http.Storage;

/// <summary>Um app cliente MCP registrado via Dynamic Client Registration (RFC 7591).</summary>
public sealed record OAuthClientRecord(string ClientId, string ClientSecret, IReadOnlyList<string> RedirectUris, string? ClientName);
