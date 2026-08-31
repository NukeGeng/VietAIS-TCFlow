using FluentValidation;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.AccessControl.Features;

public sealed class CreateProjectRoleValidator : AbstractValidator<CreateProjectRole>
{
    public CreateProjectRoleValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(2, 100);
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProjectRolePermissionsValidator : AbstractValidator<UpdateProjectRolePermissions>
{
    public UpdateProjectRolePermissionsValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Grants).NotNull();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}

public sealed class AddProjectMemberValidator : AbstractValidator<AddProjectMember>
{
    public AddProjectMemberValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}

public sealed class AssignMemberRolesValidator : AbstractValidator<AssignMemberRoles>
{
    public AssignMemberRolesValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleIds).NotNull().NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}

public sealed class RemoveProjectMemberValidator : AbstractValidator<RemoveProjectMember>
{
    public RemoveProjectMemberValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}
