using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Vue;

public sealed class VueAnalyzer : IRepositoryAnalyzer
{
    public string Name => "vue-v1";

    public bool Supports(RepositoryFile file) =>
        Path.GetExtension(file.RelativePath).ToLowerInvariant() is ".vue" or ".ts" or ".tsx";

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
}
