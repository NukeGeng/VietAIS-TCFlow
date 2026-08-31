using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;

namespace VietAIS.TCFlow.Modules.AccessControl.Tests;

public sealed class ProjectAccessAggregateTests
{
    [Fact]
    public void InitializationGivesOwnerOnlyProjectPermissions()
    {
        var projectId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var aggregate = new ProjectAccessAggregate();
        aggregate.Apply(new ProjectAccessInitialized(
            projectId,
            "owner-1",
            roleId,
            "owner-1",
            "c-1",
            DateTimeOffset.UtcNow));

        Assert.Equal("owner-1", aggregate.OwnerId);
        var owner = Assert.Single(aggregate.Members);
        Assert.Equal("owner-1", owner.UserId);
        Assert.Equal(roleId, Assert.Single(owner.RoleIds));
        Assert.All(Assert.Single(aggregate.Roles).Grants, grant =>
            Assert.Contains(grant.PermissionCode, ProjectPermissionCatalog.All));
    }

    [Fact]
    public void ProjectRoleCannotGrantUnknownOrSystemPermission()
    {
        var aggregate = Initialized();
        var role = aggregate.CreateRole("Reviewer", Guid.NewGuid(), "owner-1", "c-2", DateTimeOffset.UtcNow);
        aggregate.Apply(role);

        Assert.Throws<InvalidOperationException>(() => aggregate.UpdatePermissions(
            role.RoleId,
            [new ProjectPermissionGrant("system.user.manage", ProjectResourceScope.All)],
            "owner-1",
            "c-3",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void OwnerCannotBeRemovedOrLoseOwnerRole()
    {
        var aggregate = Initialized();
        var ownerRoleId = Assert.Single(aggregate.Members).RoleIds[0];

        Assert.Throws<InvalidOperationException>(() => aggregate.RemoveMember(
            "owner-1", "owner-1", "c-2", DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => aggregate.AssignRoles(
            "owner-1", [Guid.NewGuid()], "owner-1", "c-3", DateTimeOffset.UtcNow));
        Assert.Equal(ownerRoleId, Assert.Single(aggregate.Members).RoleIds[0]);
    }

    [Fact]
    public void DuplicateRoleNamesAndMembersAreRejected()
    {
        var aggregate = Initialized();
        var role = aggregate.CreateRole("Reviewer", Guid.NewGuid(), "owner-1", "c-2", DateTimeOffset.UtcNow);
        aggregate.Apply(role);
        Assert.Throws<InvalidOperationException>(() => aggregate.CreateRole(
            " reviewer ", Guid.NewGuid(), "owner-1", "c-3", DateTimeOffset.UtcNow));

        var member = aggregate.AddMember("user-1", "owner-1", "c-4", DateTimeOffset.UtcNow);
        aggregate.Apply(member);
        Assert.Throws<InvalidOperationException>(() => aggregate.AddMember(
            "user-1", "owner-1", "c-5", DateTimeOffset.UtcNow));
    }

    private static ProjectAccessAggregate Initialized()
    {
        var aggregate = new ProjectAccessAggregate();
        aggregate.Apply(new ProjectAccessInitialized(
            Guid.NewGuid(),
            "owner-1",
            Guid.NewGuid(),
            "owner-1",
            "c-1",
            DateTimeOffset.UtcNow));
        return aggregate;
    }
}
