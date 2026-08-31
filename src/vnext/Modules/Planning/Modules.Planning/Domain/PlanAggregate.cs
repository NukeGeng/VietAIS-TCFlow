namespace VietAIS.TCFlow.Modules.Planning.Domain;

public sealed class PlanAggregate
{
    private readonly HashSet<Guid> _requirements = [];
    private readonly HashSet<Guid> _milestones = [];

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Purpose { get; private set; }

    public void Apply(PlanCreated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Id = @event.PlanId;
        ProjectId = @event.ProjectId;
        Name = @event.Name;
        Purpose = @event.Purpose;
    }

    public void Apply(PlanRenamed @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Name = @event.Name;
    }

    public void Apply(RequirementAdded @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _requirements.Add(@event.RequirementId);
    }

    public void Apply(MilestoneAdded @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _milestones.Add(@event.MilestoneId);
    }

    public RequirementAdded AddRequirement(
        Guid requirementId,
        string title,
        string? description,
        string actorId,
        string correlationId,
        DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        title = NormalizeText(title, 2, 240, nameof(title));
        if (!_requirements.Add(requirementId))
        {
            throw new InvalidOperationException("The requirement already exists in this plan.");
        }

        _requirements.Remove(requirementId);
        return new RequirementAdded(Id, requirementId, title, NormalizeOptional(description, 1000), actorId.Trim(), correlationId.Trim(), now);
    }

    public MilestoneAdded AddMilestone(
        Guid milestoneId,
        string name,
        DateOnly? targetDate,
        string actorId,
        string correlationId,
        DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        name = NormalizeText(name, 2, 160, nameof(name));
        if (!_milestones.Add(milestoneId))
        {
            throw new InvalidOperationException("The milestone already exists in this plan.");
        }

        _milestones.Remove(milestoneId);
        return new MilestoneAdded(Id, milestoneId, name, targetDate, actorId.Trim(), correlationId.Trim(), now);
    }

    private static void ValidateActor(string actorId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static string NormalizeText(string value, int min, int max, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length < min || normalized.Length > max)
        {
            throw new ArgumentException($"Value must contain between {min} and {max} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int max)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > max)
        {
            throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value));
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
