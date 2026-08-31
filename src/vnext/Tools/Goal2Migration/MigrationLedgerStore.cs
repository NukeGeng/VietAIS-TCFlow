using System.Text.Json;

namespace VietAIS.TCFlow.Tools.Migration;

internal static class MigrationLedgerStore
{
    public static MigrationLedger Load(
        string path,
        int expectedToolVersion,
        int expectedInputSchemaVersion,
        JsonSerializerOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(path))
        {
            return MigrationLedger.Empty(expectedToolVersion, expectedInputSchemaVersion);
        }

        var ledger = JsonSerializer.Deserialize<MigrationLedger>(
            File.ReadAllText(path),
            options) ?? throw new InvalidOperationException("The migration ledger is empty.");

        ValidateVersion(ledger, expectedToolVersion, expectedInputSchemaVersion);
        ValidateEntries(ledger.Entries);
        return ledger;
    }

    public static (MigrationLedger Ledger, MigrationApplyReport Report) Apply(
        MigrationPlan plan,
        MigrationLedger ledger,
        DateTimeOffset appliedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ledger);
        ValidateVersion(ledger, plan.ToolVersion, plan.InputSchemaVersion);
        ValidateEntries(ledger.Entries);

        var existing = ledger.Entries.ToDictionary(
            entry => entry.SourceReference,
            StringComparer.Ordinal);
        var before = existing.Count;
        var appendCount = 0;
        var skipCount = 0;
        var conflicts = new List<string>();

        foreach (var operation in plan.Operations)
        {
            if (existing.TryGetValue(operation.SourceReference, out var prior))
            {
                if (!string.Equals(prior.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
                {
                    conflicts.Add(
                        $"{operation.SourceReference}: ledger hash '{prior.PayloadHash}' differs from input hash '{operation.PayloadHash}'.");
                }

                skipCount++;
                continue;
            }

            if (operation.Action == MigrationAction.Skip)
            {
                // A duplicate in the same export is already represented by
                // the first operation. It must not create a second ledger row.
                skipCount++;
                continue;
            }

            existing.Add(
                operation.SourceReference,
                new MigrationLedgerEntry(
                    operation.SourceReference,
                    operation.Kind,
                    operation.SourceId,
                    operation.PayloadHash,
                    operation.TargetId,
                    operation.TargetStream,
                    operation.TargetEventType,
                    operation.Disposition,
                    appliedAtUtc));
            appendCount++;
        }

        if (conflicts.Count > 0)
        {
            throw new InvalidOperationException(
                $"Migration ledger conflicts detected: {string.Join("; ", conflicts)}");
        }

        var result = new MigrationLedger(
            plan.ToolVersion,
            plan.InputSchemaVersion,
            existing.Values
                .OrderBy(entry => entry.SourceReference, StringComparer.Ordinal)
                .ToArray());
        var report = new MigrationApplyReport(
            plan.Operations.Count,
            appendCount,
            skipCount,
            before,
            result.Entries.Count,
            appendCount == 0 || result.Entries.Count == before + appendCount,
            []);
        return (result, report);
    }

    public static void SaveAtomic(
        string path,
        MigrationLedger ledger,
        JsonSerializerOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(options);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The migration ledger path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.{Guid.CreateVersion7():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(ledger, options));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateVersion(
        MigrationLedger ledger,
        int expectedToolVersion,
        int expectedInputSchemaVersion)
    {
        if (ledger.ToolVersion != expectedToolVersion ||
            ledger.InputSchemaVersion != expectedInputSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Migration ledger version {ledger.ToolVersion}/{ledger.InputSchemaVersion} does not match expected {expectedToolVersion}/{expectedInputSchemaVersion}.");
        }
    }

    private static void ValidateEntries(IReadOnlyList<MigrationLedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (string.IsNullOrWhiteSpace(entry.SourceReference) ||
                !references.Add(entry.SourceReference))
            {
                throw new InvalidOperationException(
                    $"Migration ledger contains a duplicate or empty source reference '{entry.SourceReference}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.PayloadHash))
            {
                throw new InvalidOperationException(
                    $"Migration ledger entry '{entry.SourceReference}' is missing its payload hash.");
            }
        }
    }
}
