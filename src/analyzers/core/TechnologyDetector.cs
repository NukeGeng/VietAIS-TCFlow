namespace VietAIS.TCFlow.Analyzers.Core;

public enum TechnologyKind
{
    Vue,
    TypeScript,
    AspNetCore,
    DotNet,
    Marten,
    Unknown
}

public sealed record TechnologyDetection(
    TechnologyKind Technology,
    EvidenceLevel EvidenceLevel,
    string Reason);

public static class TechnologyDetector
{
    public static IReadOnlyList<TechnologyDetection> Detect(RepositoryFile file)
    {
        var detections = new List<TechnologyDetection>();
        var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        if (extension == ".vue")
        {
            detections.Add(new TechnologyDetection(
                TechnologyKind.Vue,
                EvidenceLevel.Confirmed,
                "The source file uses the .vue single-file-component extension."));
        }

        if (extension is ".ts" or ".tsx" || file.Content.Contains("lang=\"ts\"", StringComparison.Ordinal))
        {
            detections.Add(new TechnologyDetection(
                TechnologyKind.TypeScript,
                EvidenceLevel.Confirmed,
                "TypeScript syntax or file extension is present."));
        }

        if (extension is ".cs" or ".csproj")
        {
            detections.Add(new TechnologyDetection(
                TechnologyKind.DotNet,
                EvidenceLevel.Confirmed,
                "The source file is a C# or MSBuild project file."));
        }

        if (file.Content.Contains("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            file.Content.Contains("MapGet(", StringComparison.Ordinal) ||
            file.Content.Contains("MapPost(", StringComparison.Ordinal))
        {
            detections.Add(new TechnologyDetection(
                TechnologyKind.AspNetCore,
                EvidenceLevel.Confirmed,
                "ASP.NET Core namespace or endpoint mapping syntax is present."));
        }

        if (file.Content.Contains("IQuerySession", StringComparison.Ordinal) ||
            file.Content.Contains("IDocumentSession", StringComparison.Ordinal) ||
            file.Content.Contains("SaveChangesAsync", StringComparison.Ordinal))
        {
            detections.Add(new TechnologyDetection(
                TechnologyKind.Marten,
                EvidenceLevel.Confirmed,
                "Marten session syntax is present."));
        }

        return detections.Count == 0
            ? [new TechnologyDetection(TechnologyKind.Unknown, EvidenceLevel.Inferred, "No supported deterministic technology signal was found.")]
            : detections;
    }
}
