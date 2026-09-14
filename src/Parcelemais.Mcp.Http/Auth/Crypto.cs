using System.Security.Cryptography;

namespace Parcelemais.Mcp.Http.Auth;

/// <summary>
/// Criptografia simétrica (AES-256-GCM) usada pra guardar ClientId/ClientSecret do Parcele+
/// em repouso no DynamoDB. Formato do texto cifrado (base64): IV(12) + TAG(16) + CIPHERTEXT.
/// </summary>
public sealed class Crypto
{
    private const int IvLength = 12;
    private const int TagLength = 16;

    private readonly byte[] _key;

    public Crypto(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("A chave de criptografia deve ter exatamente 32 bytes (64 caracteres hex).", nameof(key));
        }

        _key = key;
    }

    /// <summary>
    /// Carrega a chave de <c>MCP_ENCRYPTION_KEY</c> (64 caracteres hex). Não há fallback pra geração
    /// automática em produção — diferente do exemplo em Node, aqui a chave é sempre explícita
    /// (AWS Secrets Manager injetada como variável de ambiente pelo App Runner).
    /// </summary>
    public static Crypto FromEnvironment()
    {
        var hex = Environment.GetEnvironmentVariable("MCP_ENCRYPTION_KEY")
            ?? throw new InvalidOperationException(
                "MCP_ENCRYPTION_KEY não definida. Gere uma com: openssl rand -hex 32");

        return new Crypto(Convert.FromHexString(hex.Trim()));
    }

    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(_key, TagLength);
        aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

        var result = new byte[IvLength + TagLength + ciphertext.Length];
        iv.CopyTo(result, 0);
        tag.CopyTo(result, IvLength);
        ciphertext.CopyTo(result, IvLength + TagLength);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertextBase64)
    {
        var data = Convert.FromBase64String(ciphertextBase64);
        var iv = data.AsSpan(0, IvLength);
        var tag = data.AsSpan(IvLength, TagLength);
        var ciphertext = data.AsSpan(IvLength + TagLength);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagLength);
        aes.Decrypt(iv, ciphertext, tag, plaintext);
        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
