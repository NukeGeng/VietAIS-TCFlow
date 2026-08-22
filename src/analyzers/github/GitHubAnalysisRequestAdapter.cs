using System.Text.Json;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.GitHub;

public sealed class GitHubAnalysisRequestAdapter
{
    private const string SourceProvider = "github";
    private const int MaximumPathLength = 1000;

    public RepositoryAnalysisWorkItem DeserializeAndAdapt(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new ArgumentException("GitHub analysis request payload is required.", nameof(payload));
        }

        GitHubAnalysisRequestContract request;
        try
        {
            request = JsonSerializer.Deserialize<GitHubAnalysisRequestContract>(payload, AnalysisJson.Options)
                ?? throw new InvalidOperationException("GitHub analysis request payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"GitHub analysis request payload is invalid: {exception.Message}",
                exception);
        }

        return Adapt(request);
    }

    public RepositoryAnalysisWorkItem Adapt(GitHubAnalysisRequestContract request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentity(request.Id, "Analysis request");
        EnsureIdentity(request.ProjectId, "Project");
        EnsureIdentity(request.RepositoryId, "Repository");
        EnsureDefined(request.Trigger, "analysis trigger");
        EnsureDefined(request.Status, "analysis status");
        if (request.Status != GitHubAnalysisRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending GitHub analysis requests can be adapted.");
        }

        if (request.RequestedAt == default)
        {
            throw new InvalidOperationException("GitHub analysis request time is required.");
        }

        var requesterKind = MapRequester(request.RequestedByType, request.RequestedBy);
        var changedPaths = NormalizeChangedPaths(request.ChangedFiles ?? []);
        ValidateTriggerInvariants(request, changedPaths);
        var requestId = request.Id.ToString("D");
        var deliveryId = Optional(request.DeliveryId);
        return new RepositoryAnalysisWorkItem(
            requestId,
            request.ProjectId.ToString("D"),
            request.RepositoryId.ToString("D"),
            deliveryId ?? requestId,
            SourceProvider,
            request.FullScan ? RepositoryAnalysisKind.FullScan : RepositoryAnalysisKind.Incremental,
            MapTrigger(request.Trigger),
            Optional(request.BaseRevision),
            Optional(request.HeadRevision),
            Optional(request.Reference),
            request.PullRequestNumber,
            request.RequiresChangedFileFetch,
            changedPaths,
            request.RequestedAt,
            requesterKind,
            request.RequestedBy?.ToString("D"));
    }

    private static void ValidateTriggerInvariants(
        GitHubAnalysisRequestContract request,
        IReadOnlyCollection<RepositoryChangedPath> changedPaths)
    {
        if (request.Trigger == GitHubAnalysisTrigger.InitialScan)
        {
            if (!request.FullScan ||
                !string.IsNullOrWhiteSpace(request.DeliveryId) ||
                request.RequiresChangedFileFetch ||
                changedPaths.Count != 0 ||
                request.PullRequestNumber is not null)
            {
                throw new InvalidOperationException(
                    "Initial-scan requests must be full scans without delivery or changed-file metadata.");
            }

            return;
        }

        if (request.FullScan || string.IsNullOrWhiteSpace(request.DeliveryId))
        {
            throw new InvalidOperationException(
                "Incremental GitHub analysis requests require a delivery identity and cannot be full scans.");
        }

        if (request.Trigger == GitHubAnalysisTrigger.Push)
        {
            if (request.RequiresChangedFileFetch || request.PullRequestNumber is not null)
            {
                throw new InvalidOperationException(
                    "Push requests cannot require pull-request changed-file retrieval.");
            }

            return;
        }

        if (request.PullRequestNumber is not > 0 ||
            !request.RequiresChangedFileFetch ||
            changedPaths.Count != 0)
        {
            throw new InvalidOperationException(
                "Pull-request and merge requests require a pull-request number and deferred changed-file retrieval.");
        }
    }

    private static IReadOnlyList<RepositoryChangedPath> NormalizeChangedPaths(
        IEnumerable<GitHubChangedFileContract> files)
    {
        var changedPaths = new Dictionary<string, RepositoryChangedPath>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            ArgumentNullException.ThrowIfNull(file);
            EnsureDefined(file.Status, "changed-file status");
            var path = NormalizePath(file.Path);
            if (!changedPaths.TryAdd(path, new RepositoryChangedPath(path, MapChangeKind(file.Status))))
            {
                throw new InvalidOperationException($"Changed file path '{path}' is duplicated.");
            }
        }

        return changedPaths.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizePath(string value)
    {
        var path = Required(value, "Changed file path").Replace('\\', '/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length > MaximumPathLength ||
            path[0] == '/' ||
            (path.Length > 1 && path[1] == ':') ||
            segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "Changed file paths must be safe repository-relative paths of at most 1000 characters.");
        }

        return string.Join('/', segments);
    }

    private static RepositoryAnalysisTrigger MapTrigger(GitHubAnalysisTrigger trigger) => trigger switch
    {
        GitHubAnalysisTrigger.InitialScan => RepositoryAnalysisTrigger.InitialScan,
        GitHubAnalysisTrigger.Push => RepositoryAnalysisTrigger.Push,
        GitHubAnalysisTrigger.PullRequest => RepositoryAnalysisTrigger.PullRequest,
        GitHubAnalysisTrigger.Merge => RepositoryAnalysisTrigger.Merge,
        _ => throw new InvalidOperationException($"GitHub analysis trigger '{trigger}' is not supported.")
    };

    private static ChangeKind MapChangeKind(GitHubChangedFileStatus status) => status switch
    {
        GitHubChangedFileStatus.Added => ChangeKind.Added,
        GitHubChangedFileStatus.Modified => ChangeKind.Modified,
        GitHubChangedFileStatus.Removed => ChangeKind.Deleted,
        GitHubChangedFileStatus.Renamed => ChangeKind.Renamed,
        _ => throw new InvalidOperationException($"GitHub changed-file status '{status}' is not supported.")
    };

    private static RepositoryAnalysisRequesterKind MapRequester(string value, Guid? requestedBy)
    {
        var requestedByType = Required(value, "Requested-by type");
        if (string.Equals(requestedByType, "user", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedBy is null || requestedBy == Guid.Empty)
            {
                throw new InvalidOperationException("User-requested analysis requires a user identity.");
            }

            return RepositoryAnalysisRequesterKind.User;
        }

        if (string.Equals(requestedByType, "system", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedBy is not null)
            {
                throw new InvalidOperationException("System-requested analysis cannot carry a user identity.");
            }

            return RepositoryAnalysisRequesterKind.System;
        }

        throw new InvalidOperationException("Requested-by type must be user or system.");
    }

    private static void EnsureIdentity(Guid value, string label)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidOperationException($"{label} identity is required.");
        }
    }

    private static void EnsureDefined<T>(T value, string label)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new InvalidOperationException($"GitHub {label} is invalid.");
        }
    }

    private static string Required(string? value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{label} is required.")
            : value.Trim();

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
