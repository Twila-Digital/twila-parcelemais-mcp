using System.Security.Cryptography;
using Parcelemais.Mcp.Http.Auth;
using Xunit;

namespace Parcelemais.Mcp.Tests;

public class CryptoTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void EncryptThenDecrypt_ReturnsOriginalPlaintext()
    {
        var crypto = new Crypto(Key);
        const string plaintext = "{\"e\":\"Staging\",\"i\":\"client-id\",\"s\":\"client-secret\"}";

        var ciphertext = crypto.Encrypt(plaintext);
        var decrypted = crypto.Decrypt(ciphertext);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, ciphertext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        var crypto = new Crypto(Key);
        const string plaintext = "same-input";

        var a = crypto.Encrypt(plaintext);
        var b = crypto.Encrypt(plaintext);

        Assert.NotEqual(a, b); // IV aleatório por chamada
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var crypto = new Crypto(Key);
        var ciphertext = crypto.Encrypt("hello");
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0xFF; // corrompe o último byte (dentro da tag/ciphertext)
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<Exception>(() => crypto.Decrypt(tampered));
    }

    [Fact]
    public void Constructor_RejectsWrongKeyLength()
    {
        Assert.Throws<ArgumentException>(() => new Crypto(new byte[16]));
    }
}
