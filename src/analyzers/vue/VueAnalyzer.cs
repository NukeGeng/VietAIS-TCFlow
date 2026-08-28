using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Vue;

public sealed class VueAnalyzer : IRepositoryAnalyzer, IRepositoryAnalyzerApplicability
{
    public string Name => "vue-v1";

    public bool Supports(RepositoryFile file) =>
        Path.GetExtension(file.RelativePath).ToLowerInvariant() is ".vue" or ".ts" or ".tsx";

    public bool SupportsRepository(IReadOnlyCollection<RepositoryFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files.Any(file =>
            string.Equals(Path.GetExtension(file.RelativePath), ".vue", StringComparison.OrdinalIgnoreCase) ||
            IsVueManifest(file) ||
            file.Content.Contains("from 'vue'", StringComparison.Ordinal) ||
            file.Content.Contains("from \"vue\"", StringComparison.Ordinal) ||
            file.Content.Contains("from 'pinia'", StringComparison.Ordinal) ||
            file.Content.Contains("from \"pinia\"", StringComparison.Ordinal) ||
            file.Content.Contains("from 'vue-router'", StringComparison.Ordinal) ||
            file.Content.Contains("from \"vue-router\"", StringComparison.Ordinal));
    }

    public Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<RepositoryFile> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var accumulator = new VueAnalysisAccumulator(Name);
        var supportedFiles = files.Where(Supports).OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var types = VueTypeCatalog.Create(supportedFiles);
        foreach (var file in supportedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VueSourceParser.Parse(file, types, accumulator);
        }

        return Task.FromResult(accumulator.Build());
    }

    private static bool IsVueManifest(RepositoryFile file) =>
        string.Equals(Path.GetFileName(file.RelativePath), "package.json", StringComparison.OrdinalIgnoreCase) &&
        (file.Content.Contains("\"vue\"", StringComparison.Ordinal) ||
            file.Content.Contains("\"@vitejs/plugin-vue\"", StringComparison.Ordinal));
}
