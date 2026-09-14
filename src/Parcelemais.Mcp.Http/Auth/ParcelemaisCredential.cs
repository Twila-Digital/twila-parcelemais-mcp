using System.Text.Json.Serialization;

namespace Parcelemais.Mcp.Http.Auth;

/// <summary>Credencial do Parcele+ submetida na tela /authorize — é isso que fica criptografado no código de autorização e, decriptado, vira o access_token.</summary>
public sealed record ParcelemaisCredential(
    [property: JsonPropertyName("e")] string Environment,
    [property: JsonPropertyName("i")] string ClientId,
    [property: JsonPropertyName("s")] string ClientSecret);
