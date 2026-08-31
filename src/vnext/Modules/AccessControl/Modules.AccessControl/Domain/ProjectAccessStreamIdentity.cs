using System.Security.Cryptography;

namespace VietAIS.TCFlow.Modules.AccessControl.Domain;

public static class ProjectAccessStreamIdentity
{
    public static Guid ForProject(Guid projectId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        var hash = SHA256.HashData(projectId.ToByteArray());
        return new Guid(hash.AsSpan(0, 16));
    }
}
