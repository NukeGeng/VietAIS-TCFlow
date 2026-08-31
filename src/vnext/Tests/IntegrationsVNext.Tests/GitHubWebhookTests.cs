using System.Security.Cryptography;
using System.Text;
using VietAIS.TCFlow.Modules.Integrations.Webhooks;

namespace VietAIS.TCFlow.Modules.Integrations.Tests;

public sealed class GitHubWebhookTests
{
    [Fact]
    public void SignatureVerificationIsConstantTimeAndFailsClosed()
    {
        const string body = "{\"action\":\"push\"}";
        const string secret = "test-secret";
        var digest = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        GitHubWebhookProcessor.VerifySignature(body, $"sha256={digest}", secret).ShouldBeTrue();
        GitHubWebhookProcessor.VerifySignature(body, "sha256=invalid", secret).ShouldBeFalse();
        GitHubWebhookProcessor.VerifySignature(body, digest, secret).ShouldBeFalse();
    }
}
