using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

public sealed class AspNetAnalyzer : IRepositoryAnalyzer
{
    public string Name => "aspnet-v1";

    public bool Supports(RepositoryFile file) =>
        string.Equals(Path.GetExtension(file.RelativePath), ".cs", StringComparison.OrdinalIgnoreCase);

    public Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<RepositoryFile> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var supportedFiles = files.Where(Supports)
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var source = CSharpSourceCatalog.Create(supportedFiles);
        var routing = AspNetRoutingCatalog.Create(supportedFiles);
        var semantics = AspNetSemanticCatalog.Create(source);
        var accumulator = new AspNetAnalysisAccumulator(Name);
        foreach (var file in supportedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AspNetSourceParser.Parse(file, source, routing, semantics, accumulator);
        }

        return Task.FromResult(accumulator.Build());
    }
}
