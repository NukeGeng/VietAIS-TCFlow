using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

public sealed class AspNetAnalyzer : IRepositoryAnalyzer, IRepositoryAnalyzerApplicability
{
    public string Name => "aspnet-v1";

    public bool Supports(RepositoryFile file) =>
        string.Equals(Path.GetExtension(file.RelativePath), ".cs", StringComparison.OrdinalIgnoreCase);

    public bool SupportsRepository(IReadOnlyCollection<RepositoryFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files.Any(file =>
            file.Content.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal) ||
            file.Content.Contains("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            file.Content.Contains("WebApplication.CreateBuilder", StringComparison.Ordinal) ||
            file.Content.Contains("MapGet(", StringComparison.Ordinal) ||
            file.Content.Contains("MapPost(", StringComparison.Ordinal) ||
            file.Content.Contains("MapPut(", StringComparison.Ordinal) ||
            file.Content.Contains("MapDelete(", StringComparison.Ordinal));
    }

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
