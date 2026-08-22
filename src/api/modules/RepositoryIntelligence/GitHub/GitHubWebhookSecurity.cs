using System.Security.Cryptography;
using System.Text;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public interface IGitHubWebhookSignatureValidator
{
    bool IsValid(ReadOnlySpan<byte> payload, string? signature);
}

internal sealed class GitHubWebhookSignatureValidator(string? secret) : IGitHubWebhookSignatureValidator
{
    private readonly byte[] _secret = string.IsNullOrWhiteSpace(secret)
        ? []
        : Encoding.UTF8.GetBytes(secret);

    public bool IsValid(ReadOnlySpan<byte> payload, string? signature)
    {
        if (_secret.Length == 0 ||
            string.IsNullOrWhiteSpace(signature) ||
            !signature.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature[7..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_secret, payload);
        return supplied.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
