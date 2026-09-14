using System.Security.Cryptography;
using System.Text;
using Parcelemais.Mcp.Http.Auth;
using Xunit;

namespace Parcelemais.Mcp.Tests;

public class PkceHelperTests
{
    [Fact]
    public void Verify_MatchingVerifierAndChallenge_ReturnsTrue()
    {
        const string verifier = "a-random-code-verifier-1234567890";
        var challenge = ComputeChallenge(verifier);

        Assert.True(PkceHelper.Verify(verifier, challenge));
    }

    [Fact]
    public void Verify_WrongVerifier_ReturnsFalse()
    {
        var challenge = ComputeChallenge("original-verifier");

        Assert.False(PkceHelper.Verify("wrong-verifier", challenge));
    }

    private static string ComputeChallenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
