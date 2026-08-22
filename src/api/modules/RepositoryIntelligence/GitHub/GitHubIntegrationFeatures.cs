using FSH.Framework.Core.Exceptions;
using JasperFx;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed record RegisterGitHubInstallationCommand(
    Guid ActorId,
    Guid ProjectId,
    long InstallationId,
    long AccountId,
    string AccountLogin,
    GitHubAccountKind AccountKind,
    GitHubRepositorySelectionKind RepositorySelection)
    : IRequest<GitHubAppInstallation>;

public sealed record ConnectGitHubRepositoryCommand(
    Guid ActorId,
    Guid ProjectId,
    long InstallationId,
    long GitHubRepositoryId,
    string FullName,
    string DefaultBranch)
    : IRequest<ConnectedGitHubRepository>;

public sealed record TriggerInitialRepositoryScanCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId)
    : IRequest<RepositoryAnalysisRequest>;

public sealed record IngestGitHubWebhookCommand(
    string DeliveryId,
    string Event,
    ReadOnlyMemory<byte> Payload)
    : IRequest<GitHubWebhookReceipt>;

public sealed class RegisterGitHubInstallationHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterGitHubInstallationCommand, GitHubAppInstallation>
{
    public async Task<GitHubAppInstallation> Handle(
        RegisterGitHubInstallationCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        if (request.InstallationId <= 0 || request.AccountId <= 0)
        {
            throw new ProjectManagementValidationException(
                "GitHub installation and account identities must be positive.");
        }

        if (!Enum.IsDefined(request.AccountKind) || !Enum.IsDefined(request.RepositorySelection))
        {
            throw new ProjectManagementValidationException("GitHub installation metadata is invalid.");
        }

        var accountLogin = ValidateGitHubName(request.AccountLogin, "GitHub account login");
        var existing = await session.Query<GitHubAppInstallation>()
            .SingleOrDefaultAsync(
                installation => installation.InstallationId == request.InstallationId,
                cancellationToken);
        if (existing is not null && existing.ProjectId != request.ProjectId)
        {
            throw new ProjectManagementValidationException(
                "GitHub installation is already connected to another project.");
        }

        var now = timeProvider.GetUtcNow();
        var installation = existing is null
            ? new GitHubAppInstallation(
                Guid.NewGuid(),
                request.ProjectId,
                request.InstallationId,
                request.AccountId,
                accountLogin,
                request.AccountKind,
                request.RepositorySelection,
                GitHubInstallationStatus.Active,
                now,
                now,
                request.ActorId)
            : existing with
            {
                AccountId = request.AccountId,
                AccountLogin = accountLogin,
                AccountKind = request.AccountKind,
                RepositorySelection = request.RepositorySelection,
                Status = GitHubInstallationStatus.Active,
                UpdatedAt = now,
                UpdatedBy = request.ActorId
            };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            existing is null ? "github.installation.connect" : "github.installation.update",
            nameof(GitHubAppInstallation),
            installation.Id.ToString(),
            existing,
            installation,
            timeProvider);

        session.Store(installation);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return installation;
    }

    internal static string ValidateGitHubName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectManagementValidationException($"{label} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 150 || normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ProjectManagementValidationException($"{label} is invalid.");
        }

        return normalized;
    }
}

public sealed class ConnectGitHubRepositoryHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<ConnectGitHubRepositoryCommand, ConnectedGitHubRepository>
{
    public async Task<ConnectedGitHubRepository> Handle(
        ConnectGitHubRepositoryCommand request,
        CancellationToken cancellationToken)
    {
        var resource = new AuthorizationResourceContext(request.ProjectId);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryCreate,
            resource,
            cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            resource,
            cancellationToken);
        if (request.InstallationId <= 0 || request.GitHubRepositoryId <= 0)
        {
            throw new ProjectManagementValidationException(
                "GitHub installation and repository identities must be positive.");
        }

        var installation = await session.Query<GitHubAppInstallation>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                    item.InstallationId == request.InstallationId &&
                    item.Status == GitHubInstallationStatus.Active,
                cancellationToken)
            ?? throw new NotFoundException("Active GitHub installation not found.");
        var fullName = ValidateFullName(request.FullName);
        var defaultBranch = CreateProjectRepositoryHandler.ValidateName(
            request.DefaultBranch,
            "Default branch");
        var existingAccess = await session.Query<GitHubRepositoryAccess>()
            .SingleOrDefaultAsync(
                access => access.InstallationId == request.InstallationId &&
                    access.GitHubRepositoryId == request.GitHubRepositoryId,
                cancellationToken);
        if (existingAccess is not null)
        {
            if (existingAccess.ProjectId != request.ProjectId)
            {
                throw new ProjectManagementValidationException(
                    "GitHub repository is already selected by another project.");
            }

            var existingRepository = await session.LoadAsync<ProjectRepository>(
                existingAccess.ProjectRepositoryId,
                cancellationToken)
                ?? throw new NotFoundException("Selected project repository not found.");
            return new ConnectedGitHubRepository(existingRepository, existingAccess);
        }

        var duplicateName = await session.Query<ProjectRepository>()
            .AnyAsync(
                repository => repository.ProjectId == request.ProjectId && repository.Name == fullName,
                cancellationToken);
        if (duplicateName)
        {
            throw new ProjectManagementValidationException(
                "A repository with the same GitHub full name already exists in this project.");
        }

        var now = timeProvider.GetUtcNow();
        var repository = new ProjectRepository(
            Guid.NewGuid(),
            request.ProjectId,
            fullName,
            RepositoryProviderKind.GitHub,
            null,
            $"https://github.com/{fullName}",
            defaultBranch,
            RepositoryLifecycleStatus.Active,
            now,
            request.ActorId);
        var access = new GitHubRepositoryAccess(
            Guid.NewGuid(),
            request.ProjectId,
            repository.Id,
            installation.Id,
            installation.InstallationId,
            request.GitHubRepositoryId,
            fullName,
            IsSelected: true,
            now,
            request.ActorId);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "github.repository.select",
            nameof(GitHubRepositoryAccess),
            access.Id.ToString(),
            null,
            new { Repository = repository, Access = access },
            timeProvider);

        session.Store(repository);
        session.Store(access);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return new ConnectedGitHubRepository(repository, access);
    }

    private static string ValidateFullName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectManagementValidationException("GitHub repository full name is required.");
        }

        var parts = value.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new ProjectManagementValidationException(
                "GitHub repository full name must use the owner/repository format.");
        }

        return $"{RegisterGitHubInstallationHandler.ValidateGitHubName(parts[0], "GitHub owner")}/" +
            RegisterGitHubInstallationHandler.ValidateGitHubName(parts[1], "GitHub repository name");
    }
}

public sealed class TriggerInitialRepositoryScanHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<TriggerInitialRepositoryScanCommand, RepositoryAnalysisRequest>
{
    public async Task<RepositoryAnalysisRequest> Handle(
        TriggerInitialRepositoryScanCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.SourceAnalyze,
            new AuthorizationResourceContext(request.ProjectId, request.RepositoryId),
            cancellationToken);
        var repository = await session.LoadAsync<ProjectRepository>(request.RepositoryId, cancellationToken);
        if (repository is null || repository.ProjectId != request.ProjectId)
        {
            throw new NotFoundException("Project repository not found.");
        }

        var access = await session.Query<GitHubRepositoryAccess>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                    item.ProjectRepositoryId == request.RepositoryId &&
                    item.IsSelected,
                cancellationToken);
        if (access is null)
        {
            throw new ForbiddenException(
                "Repository is not selected within the project's GitHub App installation.");
        }

        var activeInstallation = await session.Query<GitHubAppInstallation>()
            .AnyAsync(
                installation => installation.Id == access.InstallationDocumentId &&
                    installation.ProjectId == request.ProjectId &&
                    installation.Status == GitHubInstallationStatus.Active,
                cancellationToken);
        if (!activeInstallation)
        {
            throw new ForbiddenException("GitHub App installation is not active.");
        }

        var pending = await session.Query<RepositoryAnalysisRequest>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                    item.RepositoryId == request.RepositoryId &&
                    item.Trigger == GitHubAnalysisTriggerKind.InitialScan &&
                    (item.Status == GitHubAnalysisRequestStatus.Pending ||
                        item.Status == GitHubAnalysisRequestStatus.Processing),
                cancellationToken);
        if (pending is not null)
        {
            return pending;
        }

        var analysis = new RepositoryAnalysisRequest(
            Guid.NewGuid(),
            request.ProjectId,
            request.RepositoryId,
            GitHubAnalysisTriggerKind.InitialScan,
            null,
            null,
            null,
            $"refs/heads/{repository.DefaultBranch}",
            null,
            FullScan: true,
            RequiresChangedFileFetch: false,
            [],
            GitHubAnalysisRequestStatus.Pending,
            timeProvider.GetUtcNow(),
            "user",
            request.ActorId);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "repository.analysis.initial.request",
            nameof(RepositoryAnalysisRequest),
            analysis.Id.ToString(),
            null,
            analysis,
            timeProvider);

        session.Store(analysis);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return analysis;
    }
}

public sealed class IngestGitHubWebhookHandler(
    IDocumentSession session,
    TimeProvider timeProvider)
    : IRequestHandler<IngestGitHubWebhookCommand, GitHubWebhookReceipt>
{
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<GitHubWebhookReceipt> Handle(
        IngestGitHubWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var deliveryId = ValidateDeliveryId(request.DeliveryId);
        var existing = await session.LoadAsync<GitHubWebhookDelivery>(deliveryId, cancellationToken);
        if (existing is not null)
        {
            var existingAnalysis = await session.Query<RepositoryAnalysisRequest>()
                .SingleOrDefaultAsync(item => item.DeliveryId == deliveryId, cancellationToken);
            return new GitHubWebhookReceipt(
                Accepted: true,
                Duplicate: true,
                "duplicate",
                existingAnalysis?.Id);
        }

        var parsed = GitHubWebhookPayloadParser.Parse(request.Event, request.Payload);
        var access = await session.Query<GitHubRepositoryAccess>()
            .SingleOrDefaultAsync(
                item => item.InstallationId == parsed.InstallationId &&
                    item.GitHubRepositoryId == parsed.GitHubRepositoryId &&
                    item.IsSelected,
                cancellationToken);
        if (access is null)
        {
            return new GitHubWebhookReceipt(
                Accepted: false,
                Duplicate: false,
                "repository-not-selected",
                null);
        }

        var installationActive = await session.Query<GitHubAppInstallation>()
            .AnyAsync(
                item => item.Id == access.InstallationDocumentId &&
                    item.ProjectId == access.ProjectId &&
                    item.Status == GitHubInstallationStatus.Active,
                cancellationToken);
        if (!installationActive)
        {
            return new GitHubWebhookReceipt(
                Accepted: false,
                Duplicate: false,
                "installation-inactive",
                null);
        }

        var now = timeProvider.GetUtcNow();
        var delivery = new GitHubWebhookDelivery(
            deliveryId,
            access.ProjectId,
            access.ProjectRepositoryId,
            parsed.InstallationId,
            parsed.GitHubRepositoryId,
            parsed.Event,
            parsed.Action,
            GitHubWebhookPayloadParser.ComputeSha256(request.Payload),
            now);
        var analysis = new RepositoryAnalysisRequest(
            Guid.NewGuid(),
            access.ProjectId,
            access.ProjectRepositoryId,
            parsed.Trigger,
            deliveryId,
            parsed.BaseRevision,
            parsed.HeadRevision,
            parsed.Reference,
            parsed.PullRequestNumber,
            FullScan: false,
            parsed.RequiresChangedFileFetch,
            parsed.ChangedFiles,
            GitHubAnalysisRequestStatus.Pending,
            now,
            "system",
            null);
        var audit = AuditRecordFactory.Create(
            access.ProjectId,
            SystemActorId,
            "system",
            "github.webhook.ingest",
            nameof(GitHubWebhookDelivery),
            delivery.Id,
            null,
            new
            {
                delivery.Event,
                delivery.Action,
                delivery.ProjectRepositoryId,
                AnalysisRequestId = analysis.Id
            },
            timeProvider);

        session.Insert(delivery);
        session.Store(analysis);
        session.Store(audit);
        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (DocumentAlreadyExistsException)
        {
            return new GitHubWebhookReceipt(
                Accepted: true,
                Duplicate: true,
                "duplicate",
                null);
        }

        return new GitHubWebhookReceipt(
            Accepted: true,
            Duplicate: false,
            "queued",
            analysis.Id);
    }

    private static string ValidateDeliveryId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectManagementValidationException("GitHub delivery identity is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ProjectManagementValidationException(
                "GitHub delivery identity cannot exceed 200 characters.");
        }

        return normalized;
    }
}
