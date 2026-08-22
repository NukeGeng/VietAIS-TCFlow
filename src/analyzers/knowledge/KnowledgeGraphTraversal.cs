using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed class KnowledgeGraphTraversal
{
    public KnowledgeNeighborhood FindNeighborhood(
        RepositoryKnowledgeGraph graph,
        IReadOnlyCollection<string> seedArtifactIds,
        int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(seedArtifactIds);
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Traversal depth cannot be negative.");
        }

        var artifacts = graph.Artifacts.ToDictionary(artifact => artifact.Id, StringComparer.Ordinal);
        var seeds = seedArtifactIds.Where(artifacts.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var seed in seeds)
        {
            depth[seed] = 0;
            queue.Enqueue(seed);
        }

        var adjacency = BuildAdjacency(graph.Dependencies, artifacts.Keys);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDepth = depth[current];
            if (currentDepth >= maxDepth || !adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors.Order(StringComparer.Ordinal))
            {
                if (depth.ContainsKey(neighbor))
                {
                    continue;
                }

                depth[neighbor] = currentDepth + 1;
                queue.Enqueue(neighbor);
            }
        }

        var selected = depth.Keys.ToHashSet(StringComparer.Ordinal);
        var dependencies = graph.Dependencies.Where(dependency =>
                selected.Contains(dependency.SourceArtifactId) && selected.Contains(dependency.Target))
            .Select(dependency => dependency.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new KnowledgeNeighborhood(
            seeds,
            selected.Order(StringComparer.Ordinal).ToArray(),
            dependencies,
            new SortedDictionary<string, int>(depth, StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, HashSet<string>> BuildAdjacency(
        IEnumerable<Dependency> dependencies,
        IEnumerable<string> artifactIds)
    {
        var artifacts = artifactIds.ToHashSet(StringComparer.Ordinal);
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var dependency in dependencies.Where(dependency =>
                     artifacts.Contains(dependency.SourceArtifactId) && artifacts.Contains(dependency.Target)))
        {
            AddNeighbor(adjacency, dependency.SourceArtifactId, dependency.Target);
            AddNeighbor(adjacency, dependency.Target, dependency.SourceArtifactId);
        }

        return adjacency;
    }

    private static void AddNeighbor(
        IDictionary<string, HashSet<string>> adjacency,
        string source,
        string target)
    {
        if (!adjacency.TryGetValue(source, out var neighbors))
        {
            neighbors = new HashSet<string>(StringComparer.Ordinal);
            adjacency[source] = neighbors;
        }

        neighbors.Add(target);
    }
}
