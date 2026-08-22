using System.Security.Cryptography;
using System.Text;

namespace VietAIS.TCFlow.Analyzers.Core;

public static class StableIdentity
{
    public static string Create(params string?[] parts)
    {
        var value = string.Join('|', parts.Select(part => part?.Trim() ?? string.Empty));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 12));
    }

    public static string HashContent(string? value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexStringLower(hash);
    }
}
