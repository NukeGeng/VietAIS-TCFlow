using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Converts the legacy project role/member documents into the AccessControl
/// aggregate's typed event language. Ambiguous permissions and membership
/// shapes are rejected instead of being silently broadened.
/// </summary>
internal static class ProjectAccessMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.access-control";

    public static IReadOnlyList<object> ToEvents(
        MigrationOperation operation,
        LegacyRecord record,
        LegacyRecord projectRecord)
    {
        ArgumentNullException.ThrowIfNull(projectRecord);
        return operation.Kind switch
        {
            "ProjectRole" => ToRoleEvents(operation, record, projectRecord),
            "ProjectMembership" => ToMembershipEvents(operation, record, projectRecord),
            _ => throw new InvalidOperationException(
                $"AccessControl mapper does not support migration kind '{operation.Kind}'.")
        };
    }

    public static Guid ProjectId(MigrationOperation operation) =>
        Goal2MigrationPlanner.CreateDeterministicId(
            "Project",
            RequiredProjectSourceId(operation));

    public static Guid RoleId(string sourceId) =>
        Goal2MigrationPlanner.CreateDeterministicId("ProjectRole", sourceId);

    public static Guid? TryRoleId(JsonElement value)
    {
        var raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when TryGetProperty(value, out var nested, "roleId", "id") =>
                GetScalarString(nested),
            _ => null
        };

        return string.IsNullOrWhiteSpace(raw) ? null : RoleId(raw.Trim());
    }

    private static List<object> ToRoleEvents(
        MigrationOperation operation,
        LegacyRecord record,
        LegacyRecord projectRecord)
    {
        EnsureKind(operation, record, "ProjectRole");
        var projectId = ProjectId(operation);
        var roleId = RoleId(operation.SourceId);
        var name = RequiredString(record.Payload, "name");
        var isOwner = RequiredBoolean(record.Payload, "isOwner");
        var isSystemDefined = OptionalBoolean(record.Payload, false, "isSystemDefined");
        var occurredAt = RequiredDateTime(
            record.Payload,
            "updatedAtUtc",
            "updatedAt",
            "createdAtUtc",
            "createdAt",
            "occurredAtUtc");
        var correlationId = CorrelationId(operation);

        if (isOwner)
        {
            if (!isSystemDefined || !string.Equals(name, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Legacy owner role '{operation.SourceId}' must be a system-defined Owner role.");
            }

            var ownerId = FirstRequiredString(
                record.Payload,
                projectRecord.Payload,
                "ownerId",
                "primaryOwnerId",
                "createdBy");
            var grants = ParseGrants(record.Payload);
            EnsureOwnerGrants(grants, operation.SourceId);
            return
            [
                new ProjectAccessInitialized(
                    projectId,
                    ownerId,
                    roleId,
                    MigrationActor,
                    correlationId,
                    occurredAt)
            ];
        }

        if (isSystemDefined)
        {
            throw new InvalidOperationException(
                $"Legacy system-defined role '{operation.SourceId}' is not the project Owner role.");
        }

        var events = new List<object>
        {
            new ProjectRoleCreated(
                projectId,
                roleId,
                name,
                MigrationActor,
                correlationId,
                occurredAt)
        };
        if (HasProperty(record.Payload, "permissions", "grants"))
        {
            events.Add(new ProjectRolePermissionsUpdated(
                projectId,
                roleId,
                ParseGrants(record.Payload),
                MigrationActor,
                correlationId,
                occurredAt));
        }

        return events;
    }

    private static List<object> ToMembershipEvents(
        MigrationOperation operation,
        LegacyRecord record,
        LegacyRecord projectRecord)
    {
        EnsureKind(operation, record, "ProjectMembership");
        var projectId = ProjectId(operation);
        var ownerId = FirstRequiredString(
            projectRecord.Payload,
            projectRecord.Payload,
            "ownerId",
            "primaryOwnerId",
            "createdBy");
        var userId = RequiredStringAny(record.Payload, "userId", "memberId", "actorId");
        var isActive = RequiredBoolean(record.Payload, "isActive", "active");
        var roleIds = ParseRoleIds(record.Payload);
        var occurredAt = RequiredDateTime(
            record.Payload,
            "updatedAtUtc",
            "updatedAt",
            "createdAtUtc",
            "createdAt",
            "occurredAtUtc");
        var correlationId = CorrelationId(operation);

        var ownerRoleId = FindOwnerRoleId(projectRecord, operation, roleIds);
        if (string.Equals(userId, ownerId, StringComparison.Ordinal))
        {
            if (!isActive)
            {
                throw new InvalidOperationException("The legacy project owner membership cannot be inactive.");
            }

            var ownerRoles = roleIds.Count == 0 ? [ownerRoleId] : roleIds;
            if (!ownerRoles.Contains(ownerRoleId))
            {
                throw new InvalidOperationException("The project owner membership must retain the Owner role.");
            }

            return
            [
                new ProjectMemberRolesAssigned(
                    projectId,
                    userId,
                    ownerRoles,
                    MigrationActor,
                    correlationId,
                    occurredAt)
            ];
        }

        var events = new List<object>
        {
            new ProjectMemberAdded(
                projectId,
                userId,
                MigrationActor,
                correlationId,
                occurredAt)
        };
        if (roleIds.Count > 0)
        {
            events.Add(new ProjectMemberRolesAssigned(
                projectId,
                userId,
                roleIds,
                MigrationActor,
                correlationId,
                occurredAt));
        }

        if (!isActive)
        {
            events.Add(new ProjectMemberRemoved(
                projectId,
                userId,
                MigrationActor,
                correlationId,
                occurredAt));
        }

        return events;
    }

    private static Guid FindOwnerRoleId(
        LegacyRecord projectRecord,
        MigrationOperation operation,
        List<Guid> membershipRoleIds)
    {
        // A newer export may carry the owner role source id on the project
        // record. The v0.1 shape does not, so the owner's first assigned role
        // is the only safe fallback; an empty list remains fail-closed.
        if (!TryGetProperty(projectRecord.Payload, out var value, "ownerRoleId", "ownerRoleSourceId"))
        {
            if (membershipRoleIds.Count > 0)
            {
                return membershipRoleIds[0];
            }

            throw new InvalidOperationException(
                $"Project payload for '{operation.ProjectSourceId}' and owner membership must identify the Owner role source id.");
        }

        var raw = GetScalarString(value);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Project payload for '{operation.ProjectSourceId}' has an empty Owner role source id.");
        }

        return RoleId(raw);
    }

    private static List<Guid> ParseRoleIds(JsonElement payload)
    {
        if (!TryGetProperty(payload, out var value, "roleIds", "roles"))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Legacy membership roles must be an array.");
        }

        var result = new List<Guid>();
        foreach (var item in value.EnumerateArray())
        {
            var roleId = TryRoleId(item)
                ?? throw new InvalidOperationException("Legacy membership contains an invalid role identifier.");
            if (!result.Contains(roleId))
            {
                result.Add(roleId);
            }
        }

        return result;
    }

    private static List<ProjectPermissionGrant> ParseGrants(JsonElement payload)
    {
        if (!TryGetProperty(payload, out var value, "permissions", "grants"))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Legacy role permissions must be an array.");
        }

        var grants = new List<ProjectPermissionGrant>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Legacy role permission entries must be objects.");
            }

            var permission = RequiredString(item, "permissionCode", "permission", "id");
            if (!ProjectPermissionCatalog.All.Contains(permission))
            {
                throw new InvalidOperationException(
                    $"Legacy role permission '{permission}' is not defined by the vNext project catalog.");
            }

            var scopeText = RequiredString(item, "resourceScope", "scope");
            if (!Enum.TryParse<ProjectResourceScope>(scopeText, ignoreCase: true, out var scope))
            {
                throw new InvalidOperationException(
                    $"Legacy role permission '{permission}' has unsupported resource scope '{scopeText}'.");
            }

            var resourceId = OptionalScalarString(item, "resourceId");
            if (scope == ProjectResourceScope.Repository && string.IsNullOrWhiteSpace(resourceId))
            {
                throw new InvalidOperationException(
                    $"Repository-scoped permission '{permission}' requires a resource id.");
            }

            var components = ParseComponents(item);
            var grant = new ProjectPermissionGrant(
                permission,
                scope,
                resourceId,
                components.Count == 0 ? null : components);
            if (grants.Contains(grant))
            {
                throw new InvalidOperationException(
                    $"Legacy role contains duplicate permission grant '{permission}'.");
            }

            grants.Add(grant);
        }

        return grants;
    }

    private static List<ProjectComponentScope> ParseComponents(JsonElement item)
    {
        if (!TryGetProperty(item, out var value, "componentScopes", "components"))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Legacy permission component scopes must be an array.");
        }

        var components = new List<ProjectComponentScope>();
        foreach (var entry in value.EnumerateArray())
        {
            var text = GetScalarString(entry);
            if (string.IsNullOrWhiteSpace(text) ||
                !Enum.TryParse<ProjectComponentScope>(text, ignoreCase: true, out var component))
            {
                throw new InvalidOperationException("Legacy permission contains an unsupported component scope.");
            }

            if (!components.Contains(component))
            {
                components.Add(component);
            }
        }

        return components;
    }

    private static void EnsureOwnerGrants(
        IReadOnlyList<ProjectPermissionGrant> grants,
        string sourceId)
    {
        var expected = ProjectPermissionCatalog.OwnerGrants
            .Select(GrantKey)
            .ToHashSet(StringComparer.Ordinal);
        var actual = grants
            .Select(GrantKey)
            .ToHashSet(StringComparer.Ordinal);

        // v0.1 exposed granular member/role permissions, while the vNext
        // command handlers also use the two explicit manage capabilities.
        // Treat the complete granular set as evidence for those aliases; do
        // not broaden any resource or component scope during migration.
        if (grants.Any(grant => grant.PermissionCode is
                ProjectPermissionCatalog.MemberInvite or
                ProjectPermissionCatalog.MemberRemove or
                ProjectPermissionCatalog.MemberRoleAssign))
        {
            actual.Add(GrantKey(new ProjectPermissionGrant(
                ProjectPermissionCatalog.MemberManage,
                ProjectResourceScope.Project)));
        }

        if (grants.Any(grant => grant.PermissionCode is
                ProjectPermissionCatalog.RoleView or
                ProjectPermissionCatalog.RoleCreate or
                ProjectPermissionCatalog.RoleUpdate or
                ProjectPermissionCatalog.RoleDelete))
        {
            actual.Add(GrantKey(new ProjectPermissionGrant(
                ProjectPermissionCatalog.RoleManage,
                ProjectResourceScope.Project)));
        }

        if (!expected.SetEquals(actual))
        {
            throw new InvalidOperationException(
                $"Legacy Owner role '{sourceId}' grants do not exactly match the vNext project catalog.");
        }
    }

    private static string GrantKey(ProjectPermissionGrant grant) =>
        $"{grant.PermissionCode}|{grant.ResourceScope}|{grant.ResourceId}|" +
        (grant.Components is null ? string.Empty : string.Join(',', grant.Components.Order()));

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its planned AccessControl operation.");
        }
    }

    private static string RequiredProjectSourceId(MigrationOperation operation) =>
        string.IsNullOrWhiteSpace(operation.ProjectSourceId)
            ? throw new InvalidOperationException(
                $"Migration operation '{operation.SourceReference}' must identify its project source id.")
            : operation.ProjectSourceId;

    private static string CorrelationId(MigrationOperation operation) =>
        $"migration:{operation.SourceReference}";

    private static string FirstRequiredString(
        JsonElement primary,
        JsonElement fallback,
        params string[] names)
    {
        foreach (var source in new[] { primary, fallback })
        {
            var value = OptionalString(source, names);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        throw new InvalidOperationException(
            $"Migration payload is missing one of the required identity properties: {string.Join(", ", names)}.");
    }

    private static string RequiredString(JsonElement payload, params string[] names)
    {
        var value = OptionalString(payload, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing one of the required string properties: {string.Join(", ", names)}.");
        }

        return value.Trim();
    }

    private static string RequiredStringAny(JsonElement payload, params string[] names) =>
        RequiredString(payload, names);

    private static bool RequiredBoolean(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing one of the required boolean properties: {string.Join(", ", names)}.");
        }

        return value.GetBoolean();
    }

    private static bool OptionalBoolean(JsonElement payload, bool defaultValue, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            return defaultValue;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw new InvalidOperationException(
            $"Migration payload contains an invalid boolean property: {string.Join(", ", names)}.");
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(payload, out var value, name) &&
                value.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    value.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        throw new InvalidOperationException(
            $"Migration payload is missing a valid timestamp; expected one of: {string.Join(", ", names)}.");
    }

    private static bool HasProperty(JsonElement payload, params string[] names) =>
        TryGetProperty(payload, out _, names);

    private static string? OptionalString(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            return null;
        }

        return GetScalarString(value);
    }

    private static string? OptionalScalarString(JsonElement payload, params string[] names) =>
        OptionalString(payload, names);

    private static string? GetScalarString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True or JsonValueKind.False => value.ToString(),
        _ => null
    };

    private static bool TryGetProperty(JsonElement payload, out JsonElement value, params string[] names)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var propertyValue = payload.EnumerateObject()
                .Where(item => names.Any(name =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Value)
                .FirstOrDefault();
            if (propertyValue.ValueKind != JsonValueKind.Undefined)
            {
                value = propertyValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}
