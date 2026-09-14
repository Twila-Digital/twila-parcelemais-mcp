using System.Security.Cryptography;
using System.Text;

namespace Parcelemais.Mcp.Http.Auth;

internal static class PkceHelper
{
    /// <summary>Verifica um <c>code_verifier</c> contra o <c>code_challenge</c> (método S256 — o único suportado).</summary>
    public static bool Verify(string verifier, string challenge)
    {
        var computed = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge));
    }
}
