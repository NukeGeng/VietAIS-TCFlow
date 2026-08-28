using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Identity.Users.Abstractions;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public sealed class SystemPermissionEvaluator(IUserService userService) : ISystemPermissionEvaluator
{
    public async Task EnsureAuthorizedAsync(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            !await userService.HasPermissionAsync(userId.ToString(), permissionCode, cancellationToken))
        {
            throw new ForbiddenException(
                $"System permission '{permissionCode}' is not granted for this actor.");
        }
    }
}
