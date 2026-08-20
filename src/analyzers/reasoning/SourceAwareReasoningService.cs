using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public sealed class SourceAwareReasoningService(
    IAiReasoningProvider provider,
    KnowledgeRetriever? retriever = null)
{
    private const decimal ProposedConfidenceThreshold = 0.7m;
    private readonly KnowledgeRetriever _retriever = retriever ?? new KnowledgeRetriever();

    public async Task<StructuredImpactPlan> AnalyzeAsync(
        string projectId,
        RepositoryKnowledgeGraph graph,
        IReadOnlyCollection<string> sourceChangeIds,
        AuthorityImpactDecision authority,
        RepositoryConventionProfile conventions,
        AiActionPolicy policy,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project identity is required.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(sourceChangeIds);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(conventions);
        ArgumentNullException.ThrowIfNull(policy);
        if (!string.Equals(policy.ProjectId, projectId, StringComparison.Ordinal))
        {
            throw new AiPolicyViolationException("AI policy does not belong to the requested project.");
        }

        AiActionAuthorizer.EnsureAllowed(policy, AiTaskAction.Analyze);
        var changeIds = sourceChangeIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (changeIds.Length == 0 || changeIds.Any(id => graph.Changes.All(change => change.Id != id)))
        {
            throw new ArgumentException("Every requested source change must exist in the repository graph.",
                nameof(sourceChangeIds));
        }

        var context = new AiReasoningContext(
            projectId,
            graph.RepositoryId,
            changeIds,
            _retriever.RetrieveForChanges(graph, changeIds, maxDepth),
            authority,
            conventions.Observations.Select(observation => new TargetedConventionSignal(
                    observation.Kind,
                    observation.Value,
                    observation.EvidenceLevel,
                    observation.Confidence))
                .OrderBy(observation => observation.Kind)
                .ThenBy(observation => observation.Value, StringComparer.Ordinal)
                .ToArray());
        var raw = await provider.AnalyzeImpactAsync(context, cancellationToken);
        return ValidateAndStructure(context, raw);
    }

    private static StructuredImpactPlan ValidateAndStructure(
        AiReasoningContext context,
        AiImpactReasoningResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var summary = Required(result.Summary, "Impact summary");
        var confidence = Confidence(result.Confidence, "Impact confidence");
        var availableEvidenceIds = context.GraphContext.Evidence.Select(evidence => evidence.Id)
            .Concat(context.Authority.EvidenceIds)
            .ToHashSet(StringComparer.Ordinal);
        var availableArtifactIds = context.GraphContext.Artifacts.Select(artifact => artifact.Id)
            .ToHashSet(StringComparer.Ordinal);
        var evidenceIds = ExistingIds(result.EvidenceIds, availableEvidenceIds, "impact evidence");
        var tasks = (result.Tasks ?? []).Select((task, index) =>
        {
            var taskConfidence = Confidence(task.Confidence, $"Task {index + 1} confidence");
            var taskEvidenceIds = ExistingIds(task.EvidenceIds, availableEvidenceIds, $"task {index + 1} evidence");
            var artifactIds = ExistingIds(task.ArtifactIds, availableArtifactIds, $"task {index + 1} artifacts");
            var title = Required(task.Title, $"Task {index + 1} title");
            var correlationKey = StableIdentity.Create(
                "task-correlation",
                context.RepositoryId,
                context.Authority.ContractMismatchId,
                task.TargetComponent.ToString());
            return new StructuredTaskProposal(
                StableIdentity.Create("task-proposal", correlationKey, title),
                context.ProjectId,
                context.RepositoryId,
                correlationKey,
                context.Authority.ContractMismatchId,
                title,
                Optional(task.Description),
                task.TargetComponent,
                NormalizeEvidenceLevel(task.EvidenceLevel, taskConfidence),
                taskConfidence,
                artifactIds,
                taskEvidenceIds,
                context.SourceChangeIds,
                NormalizeValues(task.Requirements),
                SourceChangeState.Active,
                TaskProposalDisposition.Suggested);
        }).ToArray();
        return new StructuredImpactPlan(
            StableIdentity.Create(
                "structured-impact",
                context.ProjectId,
                context.RepositoryId,
                context.Authority.ContractMismatchId,
                string.Join(',', context.SourceChangeIds)),
            context.ProjectId,
            context.RepositoryId,
            context.Authority.ContractMismatchId,
            summary,
            result.Severity,
            NormalizeEvidenceLevel(result.EvidenceLevel, confidence),
            confidence,
            context.SourceChangeIds,
            evidenceIds,
            tasks);
    }

    private static EvidenceLevel NormalizeEvidenceLevel(EvidenceLevel level, decimal confidence) =>
        confidence < ProposedConfidenceThreshold
            ? EvidenceLevel.Proposed
            : level == EvidenceLevel.Confirmed
                ? EvidenceLevel.Inferred
                : level;

    private static decimal Confidence(decimal value, string name)
    {
        if (value is < 0m or > 1m)
        {
            throw new InvalidOperationException($"{name} must be between 0 and 1.");
        }

        return value;
    }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> ExistingIds(
        IEnumerable<string>? values,
        IReadOnlySet<string> available,
        string name)
    {
        var normalized = NormalizeValues(values);
        if (normalized.Any(id => !available.Contains(id)))
        {
            throw new InvalidOperationException($"AI result contains {name} outside targeted context.");
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string>? values) =>
        (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}

public static class TaskGenerationService
{
    public static IReadOnlyList<StructuredTaskProposal> Prepare(
        StructuredImpactPlan impact,
        TaskGenerationMode mode,
        AiActionPolicy policy,
        decimal automaticCreationThreshold = 0.75m)
    {
        ArgumentNullException.ThrowIfNull(impact);
        ArgumentNullException.ThrowIfNull(policy);
        if (!string.Equals(impact.ProjectId, policy.ProjectId, StringComparison.Ordinal))
        {
            throw new AiPolicyViolationException("AI policy does not belong to the impact project.");
        }

        AiActionAuthorizer.EnsureAllowed(policy, AiTaskAction.Suggest);
        if (mode == TaskGenerationMode.Create)
        {
            AiActionAuthorizer.EnsureAllowed(policy, AiTaskAction.Create);
        }

        if (automaticCreationThreshold is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(automaticCreationThreshold));
        }

        return impact.Tasks.Select(task => task with
        {
            Disposition = mode == TaskGenerationMode.Create &&
                task.Confidence >= automaticCreationThreshold &&
                task.EvidenceLevel != EvidenceLevel.Proposed
                    ? TaskProposalDisposition.Create
                    : TaskProposalDisposition.Suggested
        })
            .ToArray();
    }
}
