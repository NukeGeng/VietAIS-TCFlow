using Marten;

namespace VietAIS.TCFlow.Analyzers.Governance;

public sealed record RepositoryConventionProfileDocument(
    string Id,
    RepositoryConventionProfile Profile,
    DateTimeOffset UpdatedAt);

public static class ConventionProfileStorage
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Schema.For<RepositoryConventionProfileDocument>().UseOptimisticConcurrency(true);
    }
}

public sealed class MartenConventionProfileWriter(
    IDocumentSession session,
    TimeProvider timeProvider)
{
    public async Task SaveAsync(
        RepositoryConventionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.RepositoryId))
        {
            throw new ArgumentException("Repository identity is required.", nameof(profile));
        }

        var current = await session.LoadAsync<RepositoryConventionProfileDocument>(
            profile.RepositoryId,
            cancellationToken);
        if (current is not null && profile.Revision <= current.Profile.Revision)
        {
            throw new InvalidOperationException(
                $"Convention revision {profile.Revision} is not newer than persisted revision {current.Profile.Revision}.");
        }

        session.Store(new RepositoryConventionProfileDocument(
            profile.RepositoryId,
            profile,
            timeProvider.GetUtcNow()));
        await session.SaveChangesAsync(cancellationToken);
    }
}

public sealed class MartenConventionProfileReader(IQuerySession session)
{
    public async Task<RepositoryConventionProfile?> LoadAsync(
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("Repository identity is required.", nameof(repositoryId));
        }

        var document = await session.LoadAsync<RepositoryConventionProfileDocument>(repositoryId, cancellationToken);
        return document?.Profile;
    }
}
