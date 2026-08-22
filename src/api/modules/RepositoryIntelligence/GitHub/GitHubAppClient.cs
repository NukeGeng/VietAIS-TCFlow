using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FSH.Framework.Core.Exceptions;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public interface IGitHubAppClient
{
    Uri CreateInstallationUrl(string state);

    Uri CreateUserAuthorizationUrl(string state, string codeChallenge);

    Task<GitHubRemoteInstallation> GetInstallationAsync(
        long installationId,
        CancellationToken cancellationToken);

    Task<GitHubVerifiedConnection> VerifyUserInstallationAsync(
        long installationId,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubRepositorySummary>> GetInstallationRepositoriesAsync(
        long installationId,
        CancellationToken cancellationToken);
}

public sealed record GitHubRemoteInstallation(
    long InstallationId,
    long AccountId,
    string AccountLogin,
    GitHubAccountKind AccountKind,
    GitHubRepositorySelectionKind RepositorySelection,
    bool Suspended);

public sealed record GitHubVerifiedConnection(
    GitHubRemoteInstallation Installation,
    IReadOnlyList<GitHubRepositorySummary> Repositories);

internal sealed class GitHubAppClient(
    HttpClient httpClient,
    IOptions<GitHubAppOptions> options,
    TimeProvider timeProvider) : IGitHubAppClient
{
    private const int PageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GitHubAppOptions _options = options.Value;

    public Uri CreateInstallationUrl(string state)
    {
        EnsureConfigured();
        return BuildUri(
            _options.WebBaseUrl,
            $"apps/{Uri.EscapeDataString(_options.AppSlug)}/installations/new",
            new Dictionary<string, string> { ["state"] = state });
    }

    public Uri CreateUserAuthorizationUrl(string state, string codeChallenge)
    {
        EnsureConfigured();
        return BuildUri(
            _options.WebBaseUrl,
            "login/oauth/authorize",
            new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.OAuthCallbackUrl,
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256"
            });
    }

    public async Task<GitHubRemoteInstallation> GetInstallationAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateInstallationId(installationId);
        using var request = CreateApiRequest(
            HttpMethod.Get,
            $"app/installations/{installationId.ToString(CultureInfo.InvariantCulture)}",
            CreateAppJwt());
        var response = await SendAsync<InstallationResponse>(request, cancellationToken);
        return MapInstallation(response);
    }

    public async Task<GitHubVerifiedConnection> VerifyUserInstallationAsync(
        long installationId,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateInstallationId(installationId);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(codeVerifier))
        {
            throw new ProjectManagementValidationException(
                "GitHub authorization code and PKCE verifier are required.");
        }

        var userToken = await ExchangeUserCodeAsync(code, codeVerifier, cancellationToken);
        var repositories = await GetRepositoriesAsync(
            $"user/installations/{installationId.ToString(CultureInfo.InvariantCulture)}/repositories",
            userToken,
            cancellationToken);
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        return new GitHubVerifiedConnection(installation, repositories);
    }

    public async Task<IReadOnlyList<GitHubRepositorySummary>> GetInstallationRepositoriesAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateInstallationId(installationId);
        var token = await CreateInstallationTokenAsync(installationId, cancellationToken);
        return await GetRepositoriesAsync("installation/repositories", token, cancellationToken);
    }

    private async Task<string> ExchangeUserCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var endpoint = BuildUri(_options.WebBaseUrl, "login/oauth/access_token", null);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                code = code.Trim(),
                redirect_uri = _options.OAuthCallbackUrl,
                code_verifier = codeVerifier.Trim()
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateRequestException("GitHub user authorization failed.", response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(
            JsonOptions,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.AccessToken))
        {
            throw new GitHubAppRequestException("GitHub user authorization returned no access token.");
        }

        return payload.AccessToken;
    }

    private async Task<string> CreateInstallationTokenAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        using var request = CreateApiRequest(
            HttpMethod.Post,
            $"app/installations/{installationId.ToString(CultureInfo.InvariantCulture)}/access_tokens",
            CreateAppJwt());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateRequestException(
                "GitHub installation token could not be created.",
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<InstallationTokenResponse>(
            JsonOptions,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.Token))
        {
            throw new GitHubAppRequestException("GitHub returned no installation token.");
        }

        return payload.Token;
    }

    private async Task<IReadOnlyList<GitHubRepositorySummary>> GetRepositoriesAsync(
        string path,
        string token,
        CancellationToken cancellationToken)
    {
        var repositories = new List<GitHubRepositorySummary>();
        for (var page = 1; ; page++)
        {
            using var request = CreateApiRequest(
                HttpMethod.Get,
                $"{path}?per_page={PageSize.ToString(CultureInfo.InvariantCulture)}&page={page.ToString(CultureInfo.InvariantCulture)}",
                token);
            var payload = await SendAsync<RepositoryListResponse>(request, cancellationToken);
            repositories.AddRange(payload.Repositories.Select(repository => new GitHubRepositorySummary(
                repository.Id,
                repository.Name,
                repository.FullName,
                repository.Private,
                string.IsNullOrWhiteSpace(repository.DefaultBranch) ? "main" : repository.DefaultBranch,
                repository.HtmlUrl)));
            if (payload.Repositories.Count < PageSize)
            {
                break;
            }
        }

        return repositories;
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateRequestException("GitHub API request failed.", response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new GitHubAppRequestException("GitHub API returned an empty response.");
    }

    private HttpRequestMessage CreateApiRequest(HttpMethod method, string path, string bearerToken)
    {
        var request = new HttpRequestMessage(
            method,
            BuildUri(_options.ApiBaseUrl, path, null));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", _options.ApiVersion);
        request.Headers.UserAgent.ParseAdd("VietAIS-TCFlow/1.0");
        return request;
    }

    private string CreateAppJwt()
    {
        var now = timeProvider.GetUtcNow();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", typ = "JWT" },
            JsonOptions));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
                exp = now.AddMinutes(9).ToUnixTimeSeconds(),
                iss = _options.AppId.ToString(CultureInfo.InvariantCulture)
            },
            JsonOptions));
        var unsignedToken = $"{header}.{payload}";
        byte[] privateKey;
        try
        {
            privateKey = Convert.FromBase64String(_options.PrivateKeyBase64);
        }
        catch (FormatException exception)
        {
            throw new GitHubAppConfigurationException(
                "GitHub App private key must be a base64-encoded PEM value.",
                exception);
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(Encoding.UTF8.GetString(privateKey));
        }
        catch (ArgumentException exception)
        {
            throw new GitHubAppConfigurationException(
                "GitHub App private key is not a valid PEM RSA key.",
                exception);
        }

        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static GitHubRemoteInstallation MapInstallation(InstallationResponse response)
    {
        if (response.Id <= 0 || response.Account is null || response.Account.Id <= 0 ||
            string.IsNullOrWhiteSpace(response.Account.Login))
        {
            throw new GitHubAppRequestException("GitHub installation metadata is incomplete.");
        }

        return new GitHubRemoteInstallation(
            response.Id,
            response.Account.Id,
            response.Account.Login,
            string.Equals(response.Account.Type, "Organization", StringComparison.OrdinalIgnoreCase)
                ? GitHubAccountKind.Organization
                : GitHubAccountKind.User,
            string.Equals(response.RepositorySelection, "all", StringComparison.OrdinalIgnoreCase)
                ? GitHubRepositorySelectionKind.All
                : GitHubRepositorySelectionKind.Selected,
            response.SuspendedAt is not null);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new GitHubAppConfigurationException(
                "GitHub App is not configured. AppId, AppSlug, ClientId, ClientSecret, " +
                "PrivateKeyBase64, and OAuthCallbackUrl are required.");
        }
    }

    private static void ValidateInstallationId(long installationId)
    {
        if (installationId <= 0)
        {
            throw new ProjectManagementValidationException(
                "GitHub installation identity must be positive.");
        }
    }

    private static Uri BuildUri(
        string baseUrl,
        string path,
        IReadOnlyDictionary<string, string>? query)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new GitHubAppConfigurationException("GitHub base URL is invalid.");
        }

        var builder = new UriBuilder(new Uri(baseUri, path));
        if (query is not null)
        {
            builder.Query = string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        return builder.Uri;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static GitHubAppRequestException CreateRequestException(
        string message,
        HttpStatusCode statusCode) =>
        new($"{message} GitHub returned HTTP {(int)statusCode}.");

    private sealed record InstallationResponse(
        long Id,
        AccountResponse? Account,
        [property: JsonPropertyName("repository_selection")] string RepositorySelection,
        [property: JsonPropertyName("suspended_at")] DateTimeOffset? SuspendedAt);

    private sealed record AccountResponse(long Id, string Login, string Type);

    private sealed record OAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record InstallationTokenResponse(string Token);

    private sealed record RepositoryListResponse(
        [property: JsonPropertyName("repositories")] List<RepositoryResponse> Repositories);

    private sealed record RepositoryResponse(
        long Id,
        string Name,
        [property: JsonPropertyName("full_name")] string FullName,
        bool Private,
        [property: JsonPropertyName("default_branch")] string DefaultBranch,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}

public sealed class GitHubAppConfigurationException : FshException
{
    public GitHubAppConfigurationException(string message, Exception? innerException = null)
        : base(message, [], HttpStatusCode.ServiceUnavailable)
    {
        if (innerException is not null)
        {
            Data[nameof(innerException)] = innerException.GetType().Name;
        }
    }
}

public sealed class GitHubAppRequestException(string message)
    : FshException(message, [], HttpStatusCode.BadGateway);
