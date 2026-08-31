using FluentValidation;
using VietAIS.TCFlow.Modules.Planning.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.Planning.Features;

public sealed class CreatePlanValidator : AbstractValidator<CreatePlan>
{
    public CreatePlanValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(2, 160);
        RuleFor(command => command.Purpose).MaximumLength(1000);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}

public sealed class AddRequirementValidator : AbstractValidator<AddRequirement>
{
    public AddRequirementValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().Length(2, 240);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}

public sealed class AddMilestoneValidator : AbstractValidator<AddMilestone>
{
    public AddMilestoneValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.MilestoneId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(2, 160);
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}
