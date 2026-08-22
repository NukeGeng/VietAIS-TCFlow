using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record GetAuthorityPolicyQuery(Guid ActorId, Guid ProjectId)
    : IRequest<AuthorityPolicy>;

public sealed record UpdateAuthorityPolicyCommand(
    Guid ActorId,
    Guid ProjectId,
    AuthorityRule[] Rules)
    : IRequest<AuthorityPolicy>;

public sealed record GetConventionProfileQuery(Guid ActorId, Guid ProjectId)
    : IRequest<ConventionProfile>;

public sealed record UpdateConventionProfileCommand(
    Guid ActorId,
    Guid ProjectId,
    ConventionProfileStatus Status,
    string[] Architectures,
    string[] ApiStyles,
    string[] PersistencePatterns,
    string[] ValidationPatterns,
    string[] DtoPatterns)
    : IRequest<ConventionProfile>;

public sealed class GetAuthorityPolicyHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetAuthorityPolicyQuery, AuthorityPolicy>
{
    public async Task<AuthorityPolicy> Handle(
        GetAuthorityPolicyQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.AuthorityView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return await session.LoadAsync<AuthorityPolicy>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Authority policy not found.");
    }
}

public sealed class UpdateAuthorityPolicyHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAuthorityPolicyCommand, AuthorityPolicy>
{
    public async Task<AuthorityPolicy> Handle(
        UpdateAuthorityPolicyCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.AuthorityUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var rules = ValidateRules(request.Rules);
        var current = await session.LoadAsync<AuthorityPolicy>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Authority policy not found.");
        var updated = current with
        {
            Rules = rules,
            UpdatedAt = timeProvider.GetUtcNow(),
            UpdatedBy = request.ActorId
        };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "authority.policy.update",
            nameof(AuthorityPolicy),
            current.Id.ToString(),
            current,
            updated,
            timeProvider);

        session.Store(updated);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static AuthorityRule[] ValidateRules(AuthorityRule[]? rules)
    {
        if (rules is null)
        {
            throw new ProjectManagementValidationException("Authority rules are required.");
        }

        var knowledgeKinds = Enum.GetValues<AuthorityKnowledgeKind>();
        if (rules.Length != knowledgeKinds.Length ||
            Array.Exists(rules, rule => rule is null) ||
            Array.Exists(rules, rule => !Enum.IsDefined(rule.Knowledge) || !Enum.IsDefined(rule.Source)) ||
            rules.Select(rule => rule.Knowledge).Distinct().Count() != knowledgeKinds.Length)
        {
            throw new ProjectManagementValidationException(
                "Authority policy must define exactly one valid rule for every knowledge kind.");
        }

        return rules.OrderBy(rule => rule.Knowledge).ToArray();
    }
}

public sealed class GetConventionProfileHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetConventionProfileQuery, ConventionProfile>
{
    public async Task<ConventionProfile> Handle(
        GetConventionProfileQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ConventionView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return await session.LoadAsync<ConventionProfile>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Convention profile not found.");
    }
}

public sealed class UpdateConventionProfileHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateConventionProfileCommand, ConventionProfile>
{
    public async Task<ConventionProfile> Handle(
        UpdateConventionProfileCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ConventionUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        if (!Enum.IsDefined(request.Status))
        {
            throw new ProjectManagementValidationException("Convention profile status is invalid.");
        }

        var current = await session.LoadAsync<ConventionProfile>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Convention profile not found.");
        var updated = current with
        {
            Status = request.Status,
            Architectures = NormalizePatterns(request.Architectures, "architecture"),
            ApiStyles = NormalizePatterns(request.ApiStyles, "API style"),
            PersistencePatterns = NormalizePatterns(request.PersistencePatterns, "persistence"),
            ValidationPatterns = NormalizePatterns(request.ValidationPatterns, "validation"),
            DtoPatterns = NormalizePatterns(request.DtoPatterns, "DTO"),
            UpdatedAt = timeProvider.GetUtcNow(),
            UpdatedBy = request.ActorId
        };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "convention.profile.update",
            nameof(ConventionProfile),
            current.Id.ToString(),
            current,
            updated,
            timeProvider);

        session.Store(updated);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static string[] NormalizePatterns(string[]? patterns, string name)
    {
        if (patterns is null)
        {
            throw new ProjectManagementValidationException($"Convention {name} patterns are required.");
        }

        if (patterns.Length > 100)
        {
            throw new ProjectManagementValidationException(
                $"Convention {name} patterns cannot contain more than 100 values.");
        }

        var normalized = patterns.Select(pattern => pattern?.Trim()).ToArray();
        if (Array.Exists(normalized, pattern => string.IsNullOrWhiteSpace(pattern) || pattern.Length > 200))
        {
            throw new ProjectManagementValidationException(
                $"Convention {name} patterns must contain between 1 and 200 characters.");
        }

        return normalized.Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
