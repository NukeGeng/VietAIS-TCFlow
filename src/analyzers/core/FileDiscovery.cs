namespace VietAIS.TCFlow.Analyzers.Core;

public sealed record FileDiscoveryOptions(
    IReadOnlySet<string>? IncludedExtensions = null,
    IReadOnlySet<string>? IgnoredDirectories = null)
{
    public IReadOnlySet<string> EffectiveIgnoredDirectories { get; } = IgnoredDirectories ??
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".idea",
            ".vscode",
            "node_modules",
            "bin",
            "obj",
            "dist",
            "coverage",
            "TestResults"
        };
}

public sealed class FileDiscovery
{
    public async Task<IReadOnlyList<RepositoryFile>> DiscoverAsync(
        string repositoryRoot,
        FileDiscoveryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Repository root '{root}' does not exist.");
        }

        options ??= new FileDiscoveryOptions();
        var files = new List<RepositoryFile>();
        foreach (var path in EnumerateFiles(root, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            files.Add(new RepositoryFile(Normalize(Path.GetRelativePath(root, path)), path, content));
        }

        return files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateFiles(string directory, FileDiscoveryOptions options)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            if (options.IncludedExtensions is null ||
                options.IncludedExtensions.Contains(Path.GetExtension(file)))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (options.EffectiveIgnoredDirectories.Contains(Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(child, options))
            {
                yield return file;
            }
        }
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
