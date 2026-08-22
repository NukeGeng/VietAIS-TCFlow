using System.ComponentModel.DataAnnotations;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed class GitHubAppOptions
{
    public const string SectionName = "GitHub";

    [Range(1, long.MaxValue)]
    public long AppId { get; set; }

    public string AppSlug { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string PrivateKeyBase64 { get; set; } = string.Empty;

    public string OAuthCallbackUrl { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    public string WebBaseUrl { get; set; } = "https://github.com";

    public string ApiVersion { get; set; } = "2022-11-28";

    public bool IsConfigured =>
        AppId > 0 &&
        !string.IsNullOrWhiteSpace(AppSlug) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(PrivateKeyBase64) &&
        Uri.TryCreate(OAuthCallbackUrl, UriKind.Absolute, out _);
}
