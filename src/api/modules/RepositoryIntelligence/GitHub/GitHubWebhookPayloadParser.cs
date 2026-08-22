using System.Security.Cryptography;
using System.Text.Json;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

internal static class GitHubWebhookPayloadParser
{
    private const int MaximumChangedFiles = 1000;

    public static ParsedGitHubWebhook Parse(string eventName, ReadOnlyMemory<byte> payload)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ProjectManagementValidationException("GitHub event name is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var installationId = RequiredInt64(root, "installation", "id");
            var repositoryId = RequiredInt64(root, "repository", "id");
            if (string.Equals(eventName.Trim(), "push", StringComparison.OrdinalIgnoreCase))
            {
                return ParsePush(root, installationId, repositoryId);
            }

            if (string.Equals(eventName.Trim(), "pull_request", StringComparison.OrdinalIgnoreCase))
            {
                return ParsePullRequest(root, installationId, repositoryId);
            }

            throw new ProjectManagementValidationException(
                $"GitHub event '{eventName}' is not supported.");
        }
        catch (JsonException exception)
        {
            throw new ProjectManagementValidationException(
                $"GitHub webhook payload is invalid JSON: {exception.Message}");
        }
    }

    public static string ComputeSha256(ReadOnlyMemory<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload.Span));

    private static ParsedGitHubWebhook ParsePush(
        JsonElement root,
        long installationId,
        long repositoryId)
    {
        var files = new Dictionary<string, GitHubChangedFileStatus>(StringComparer.Ordinal);
        if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
        {
            foreach (var commit in commits.EnumerateArray())
            {
                AddFiles(commit, "added", GitHubChangedFileStatus.Added, files);
                AddFiles(commit, "modified", GitHubChangedFileStatus.Modified, files);
                AddFiles(commit, "removed", GitHubChangedFileStatus.Removed, files);
            }
        }

        return new ParsedGitHubWebhook(
            installationId,
            repositoryId,
            "push",
            "push",
            GitHubAnalysisTriggerKind.Push,
            OptionalString(root, "before"),
            OptionalString(root, "after"),
            OptionalString(root, "ref"),
            null,
            RequiresChangedFileFetch: false,
            files.OrderBy(file => file.Key, StringComparer.Ordinal)
                .Select(file => new GitHubChangedFile(file.Key, file.Value))
                .ToArray());
    }

    private static ParsedGitHubWebhook ParsePullRequest(
        JsonElement root,
        long installationId,
        long repositoryId)
    {
        var action = OptionalString(root, "action") ?? "unknown";
        var pullRequest = root.TryGetProperty("pull_request", out var value) &&
            value.ValueKind == JsonValueKind.Object
                ? value
                : throw new ProjectManagementValidationException(
                    "GitHub pull request payload is missing pull_request metadata.");
        var merged = pullRequest.TryGetProperty("merged", out var mergedValue) &&
            mergedValue.ValueKind == JsonValueKind.True;
        var number = root.TryGetProperty("number", out var numberValue) && numberValue.TryGetInt32(out var parsed)
            ? parsed
            : (int?)null;
        return new ParsedGitHubWebhook(
            installationId,
            repositoryId,
            "pull_request",
            action,
            merged ? GitHubAnalysisTriggerKind.Merge : GitHubAnalysisTriggerKind.PullRequest,
            NestedOptionalString(pullRequest, "base", "sha"),
            NestedOptionalString(pullRequest, "head", "sha"),
            NestedOptionalString(pullRequest, "base", "ref"),
            number,
            RequiresChangedFileFetch: true,
            []);
    }

    private static void AddFiles(
        JsonElement commit,
        string property,
        GitHubChangedFileStatus status,
        Dictionary<string, GitHubChangedFileStatus> files)
    {
        if (!commit.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var value in values.EnumerateArray())
        {
            var path = value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(path) || path.Length > 1000)
            {
                throw new ProjectManagementValidationException(
                    "GitHub changed file paths must contain between 1 and 1000 characters.");
            }

            files[path] = status;
            if (files.Count > MaximumChangedFiles)
            {
                throw new ProjectManagementValidationException(
                    $"GitHub webhook cannot contain more than {MaximumChangedFiles} changed files.");
            }
        }
    }

    private static long RequiredInt64(JsonElement root, string objectName, string propertyName)
    {
        if (root.TryGetProperty(objectName, out var nested) &&
            nested.ValueKind == JsonValueKind.Object &&
            nested.TryGetProperty(propertyName, out var value) &&
            value.TryGetInt64(out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        throw new ProjectManagementValidationException(
            $"GitHub webhook payload is missing a valid {objectName}.{propertyName}.");
    }

    private static string? OptionalString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NestedOptionalString(
        JsonElement root,
        string objectName,
        string propertyName) =>
        root.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? OptionalString(nested, propertyName)
            : null;
}
