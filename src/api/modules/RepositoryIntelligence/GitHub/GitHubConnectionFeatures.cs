using System.Security.Cryptography;
using System.Text;
using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed record StartGitHubConnectionCommand(Guid ActorId, Guid ProjectId)
    : IRequest<GitHubInstallationStart>;

public sealed record PrepareGitHubAuthorizationCommand(
    Guid ActorId,
    string State,
    long InstallationId)
    : IRequest<GitHubAuthorizationStart>;

public sealed record CompleteGitHubConnectionCommand(
    Guid ActorId,
    string State,
    string Code,
    string CodeVerifier)
    : IRequest<GitHubConnectionResult>;

public sealed record GetGitHubInstallationsQuery(Guid ActorId, Guid ProjectId)
    : IRequest<IReadOnlyList<GitHubAppInstallation>>;

public sealed record GetGitHubRepositoriesQuery(
    Guid ActorId,
    Guid ProjectId,
    long InstallationId)
    : IRequest<IReadOnlyList<GitHubRepositorySummary>>;

public sealed class StartGitHubConnectionHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    IGitHubAppClient gitHub,
    TimeProvider timeProvider)
    : IRequestHandler<StartGitHubConnectionCommand, GitHubInstallationStart>
{
    public async Task<GitHubInstallationStart> Handle(
        StartGitHubConnectionCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var state = GitHubConnectionSecurity.CreateSecret();
        var installationUrl = gitHub.CreateInstallationUrl(state);
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(10);
        var attempt = new GitHubConnectionAttempt(
            GitHubConnectionSecurity.Hash(state),
            request.ProjectId,
            request.ActorId,
            GitHubConnectionStage.Installation,
            null,
            null,
            expiresAt,
            null);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "github.connection.start",
            nameof(GitHubConnectionAttempt),
            attempt.Id,
            null,
            new { attempt.Stage, attempt.ExpiresAt },
            timeProvider);

        session.Store(attempt);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return new GitHubInstallationStart(installationUrl.ToString(), expiresAt);
    }
}

public sealed class PrepareGitHubAuthorizationHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    IGitHubAppClient gitHub,
    TimeProvider timeProvider)
    : IRequestHandler<PrepareGitHubAuthorizationCommand, GitHubAuthorizationStart>
{
    public async Task<GitHubAuthorizationStart> Handle(
        PrepareGitHubAuthorizationCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await GitHubConnectionSecurity.LoadValidAttemptAsync(
            session,
            request.State,
            request.ActorId,
            GitHubConnectionStage.Installation,
            now,
            cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(attempt.ProjectId),
            cancellationToken);

        var installation = await gitHub.GetInstallationAsync(request.InstallationId, cancellationToken);
        if (installation.Suspended)
        {
            throw new ForbiddenException("GitHub App installation is suspended.");
        }

        var verifier = GitHubConnectionSecurity.CreateSecret();
        var challenge = GitHubConnectionSecurity.CreateCodeChallenge(verifier);
        var oauthState = GitHubConnectionSecurity.CreateSecret();
        var expiresAt = now.AddMinutes(10);
        var authorizationAttempt = new GitHubConnectionAttempt(
            GitHubConnectionSecurity.Hash(oauthState),
            attempt.ProjectId,
            request.ActorId,
            GitHubConnectionStage.UserAuthorization,
            installation.InstallationId,
            challenge,
            expiresAt,
            null);
        var consumedAttempt = attempt with { ConsumedAt = now };
        var audit = AuditRecordFactory.Create(
            attempt.ProjectId,
            request.ActorId,
            "user",
            "github.connection.authorization.start",
            nameof(GitHubAppInstallation),
            installation.InstallationId.ToString(),
            null,
            new
            {
                installation.InstallationId,
                installation.AccountId,
                installation.AccountLogin,
                authorizationAttempt.ExpiresAt
            },
            timeProvider);

        session.Store(consumedAttempt);
        session.Store(authorizationAttempt);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return new GitHubAuthorizationStart(
            attempt.ProjectId,
            gitHub.CreateUserAuthorizationUrl(oauthState, challenge).ToString(),
            oauthState,
            verifier,
            expiresAt);
    }
}

public sealed class CompleteGitHubConnectionHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    IGitHubAppClient gitHub,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteGitHubConnectionCommand, GitHubConnectionResult>
{
    public async Task<GitHubConnectionResult> Handle(
        CompleteGitHubConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await GitHubConnectionSecurity.LoadValidAttemptAsync(
            session,
            request.State,
            request.ActorId,
            GitHubConnectionStage.UserAuthorization,
            now,
            cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(attempt.ProjectId),
            cancellationToken);
        if (attempt.InstallationId is null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(attempt.CodeChallenge ?? string.Empty),
                Encoding.ASCII.GetBytes(GitHubConnectionSecurity.CreateCodeChallenge(request.CodeVerifier))))
        {
            throw new ProjectManagementValidationException("GitHub PKCE verifier is invalid.");
        }

        var verified = await gitHub.VerifyUserInstallationAsync(
            attempt.InstallationId.Value,
            request.Code,
            request.CodeVerifier,
            cancellationToken);
        if (verified.Installation.Suspended)
        {
            throw new ForbiddenException("GitHub App installation is suspended.");
        }

        var existing = await session.Query<GitHubAppInstallation>()
            .SingleOrDefaultAsync(
                item => item.InstallationId == verified.Installation.InstallationId,
                cancellationToken);
        if (existing is not null && existing.ProjectId != attempt.ProjectId)
        {
            throw new ProjectManagementValidationException(
                "GitHub installation is already connected to another project.");
        }

        var remote = verified.Installation;
        var installation = existing is null
            ? new GitHubAppInstallation(
                Guid.NewGuid(),
                attempt.ProjectId,
                remote.InstallationId,
                remote.AccountId,
                remote.AccountLogin,
                remote.AccountKind,
                remote.RepositorySelection,
                GitHubInstallationStatus.Active,
                now,
                now,
                request.ActorId)
            : existing with
            {
                AccountId = remote.AccountId,
                AccountLogin = remote.AccountLogin,
                AccountKind = remote.AccountKind,
                RepositorySelection = remote.RepositorySelection,
                Status = GitHubInstallationStatus.Active,
                UpdatedAt = now,
                UpdatedBy = request.ActorId
            };
        var consumedAttempt = attempt with { ConsumedAt = now };
        var audit = AuditRecordFactory.Create(
            attempt.ProjectId,
            request.ActorId,
            "user",
            existing is null ? "github.installation.connect" : "github.installation.update",
            nameof(GitHubAppInstallation),
            installation.Id.ToString(),
            existing,
            installation,
            timeProvider);

        session.Store(consumedAttempt);
        session.Store(installation);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return new GitHubConnectionResult(attempt.ProjectId, installation, verified.Repositories);
    }
}

public sealed class GetGitHubInstallationsHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetGitHubInstallationsQuery, IReadOnlyList<GitHubAppInstallation>>
{
    public async Task<IReadOnlyList<GitHubAppInstallation>> Handle(
        GetGitHubInstallationsQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        return await session.Query<GitHubAppInstallation>()
            .Where(item => item.ProjectId == request.ProjectId &&
                item.Status == GitHubInstallationStatus.Active)
            .OrderBy(item => item.AccountLogin)
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetGitHubRepositoriesHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator,
    IGitHubAppClient gitHub)
    : IRequestHandler<GetGitHubRepositoriesQuery, IReadOnlyList<GitHubRepositorySummary>>
{
    public async Task<IReadOnlyList<GitHubRepositorySummary>> Handle(
        GetGitHubRepositoriesQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryAccessManage,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        var installation = await session.Query<GitHubAppInstallation>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                    item.InstallationId == request.InstallationId &&
                    item.Status == GitHubInstallationStatus.Active,
                cancellationToken)
            ?? throw new NotFoundException("Active GitHub installation not found.");
        return await gitHub.GetInstallationRepositoriesAsync(
            installation.InstallationId,
            cancellationToken);
    }
}

internal static class GitHubConnectionSecurity
{
    public static string CreateSecret() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ProjectManagementValidationException("GitHub connection state is required.");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.Trim())));
    }

    public static string CreateCodeChallenge(string verifier)
    {
        if (string.IsNullOrWhiteSpace(verifier))
        {
            throw new ProjectManagementValidationException("GitHub PKCE verifier is required.");
        }

        return Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier.Trim())));
    }

    public static async Task<GitHubConnectionAttempt> LoadValidAttemptAsync(
        IDocumentSession session,
        string state,
        Guid actorId,
        GitHubConnectionStage stage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var attempt = await session.LoadAsync<GitHubConnectionAttempt>(Hash(state), cancellationToken)
            ?? throw new ProjectManagementValidationException("GitHub connection state is invalid.");
        if (attempt.ActorId != actorId || attempt.Stage != stage)
        {
            throw new ProjectManagementValidationException("GitHub connection state is invalid.");
        }

        if (attempt.ConsumedAt is not null)
        {
            throw new ProjectManagementValidationException("GitHub connection state was already used.");
        }

        if (attempt.ExpiresAt <= now)
        {
            throw new ProjectManagementValidationException("GitHub connection state has expired.");
        }

        return attempt;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
