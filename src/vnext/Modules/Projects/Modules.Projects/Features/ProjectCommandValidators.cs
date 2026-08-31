using FluentValidation;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.Projects.Features;

public sealed class CreateProjectValidator : AbstractValidator<CreateProject>
{
    public CreateProjectValidator()
    {
        RuleFor(command => command.Name).NotEmpty().Length(2, 150);
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}

public sealed class RenameProjectValidator : AbstractValidator<RenameProject>
{
    public RenameProjectValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(2, 150);
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}

public sealed class SuspendProjectValidator : AbstractValidator<SuspendProject>
{
    public SuspendProjectValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}

public sealed class ActivateProjectValidator : AbstractValidator<ActivateProject>
{
    public ActivateProjectValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}
