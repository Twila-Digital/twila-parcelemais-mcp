namespace Parcelemais.Mcp.Http.Storage;

/// <summary>
/// Um código de autorização de curta duração (5 min). <see cref="CredentialEnc"/> guarda
/// "ClientId:ClientSecret" do Parcele+ (submetido na tela /authorize), criptografado em repouso —
/// vira o próprio access_token (decriptado) na troca em /token.
/// </summary>
public sealed record AuthCodeRecord(
    string Code,
    string ClientId,
    string RedirectUri,
    string CodeChallenge,
    string CredentialEnc,
    long ExpiresAtUnixSeconds,
    string? Scope,
    string? Resource);
