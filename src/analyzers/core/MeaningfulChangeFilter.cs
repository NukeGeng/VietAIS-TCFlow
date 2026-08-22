using System.Text.RegularExpressions;

namespace VietAIS.TCFlow.Analyzers.Core;

public enum ChangeDecision
{
    CosmeticOnly,
    NonBehavioral,
    Meaningful
}

public sealed record SourceFileChange(
    string Path,
    string? Before,
    string? After,
    ChangeKind Kind = ChangeKind.Modified);

public sealed record ChangeFilterResult(
    ChangeDecision Decision,
    bool HasCrossLayerPotential,
    int RecommendedAiRequests,
    string Reason,
    SourceChange Change);

public sealed partial class MeaningfulChangeFilter
{
    private static readonly string[] CrossLayerSignals =
    [
        "fetch(",
        "apiRequest",
        ".get(",
        ".post(",
        ".put(",
        ".delete(",
        "defineProps",
        "defineEmits",
        "v-model",
        "defineStore",
        "path:",
        "hasPermission",
        "permission",
        "pageNumber",
        "pageSize",
        "MapGet(",
        "MapPost(",
        "MapPut(",
        "MapDelete(",
        "MapMethods(",
        "RequireAuthorization",
        "IQuerySession",
        "IDocumentSession",
        "SaveChangesAsync(",
        "session.Store(",
        "session.Delete("
    ];

    public ChangeFilterResult Evaluate(SourceFileChange fileChange)
    {
        ArgumentNullException.ThrowIfNull(fileChange);
        var before = NormalizeLines(fileChange.Before);
        var after = NormalizeLines(fileChange.After);
        var path = fileChange.Path.Replace('\\', '/');

        if (NormalizeCode(before) == NormalizeCode(after))
        {
            return CreateResult(
                fileChange,
                ChangeDecision.NonBehavioral,
                false,
                0,
                "Only whitespace or line-ending changes were detected.");
        }

        var extension = Path.GetExtension(path);
        if (extension is ".css" or ".scss" or ".sass" or ".less")
        {
            return CreateResult(
                fileChange,
                ChangeDecision.CosmeticOnly,
                false,
                0,
                "Stylesheet-only changes do not have cross-layer impact in Vue Analyzer V1.");
        }

        if (string.Equals(extension, ".vue", StringComparison.OrdinalIgnoreCase) &&
            NormalizeCode(RemoveStyleBlocks(before)) == NormalizeCode(RemoveStyleBlocks(after)))
        {
            return CreateResult(
                fileChange,
                ChangeDecision.CosmeticOnly,
                false,
                0,
                "The Vue change is confined to style blocks.");
        }

        if (extension is ".md" or ".txt")
        {
            return CreateResult(
                fileChange,
                ChangeDecision.NonBehavioral,
                false,
                0,
                "Documentation-only changes are not cross-layer source changes.");
        }

        var changedText = $"{before}\n{after}";
        var crossLayer = CrossLayerSignals.Any(signal => changedText.Contains(signal, StringComparison.Ordinal));
        return CreateResult(
            fileChange,
            ChangeDecision.Meaningful,
            crossLayer,
            crossLayer ? 1 : 0,
            crossLayer
                ? "The change contains deterministic contract, state, routing, or permission signals."
                : "Executable source changed and requires deterministic re-analysis.");
    }

    private static ChangeFilterResult CreateResult(
        SourceFileChange source,
        ChangeDecision decision,
        bool crossLayer,
        int recommendedAiRequests,
        string reason)
    {
        var path = source.Path.Replace('\\', '/');
        var change = new SourceChange(
            StableIdentity.Create("change", path, source.Kind.ToString(), source.Before, source.After),
            path,
            source.Kind,
            StableIdentity.HashContent(source.Before),
            StableIdentity.HashContent(source.After),
            decision == ChangeDecision.Meaningful,
            reason);
        return new ChangeFilterResult(decision, crossLayer, recommendedAiRequests, reason, change);
    }

    private static string RemoveStyleBlocks(string value) => StyleBlockRegex().Replace(value, string.Empty);

    private static string NormalizeLines(string? value) =>
        (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string NormalizeCode(string value) => WhitespaceRegex().Replace(value, string.Empty);

    [GeneratedRegex(@"<style\b[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
