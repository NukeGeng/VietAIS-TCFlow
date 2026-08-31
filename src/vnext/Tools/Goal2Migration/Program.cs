using System.Text.Json;
using System.Text.Json.Serialization;

namespace VietAIS.TCFlow.Tools.Migration;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

    private static async Task<int> MainAsync(string[] args)
    {
        try
        {
            var inputPath = ReadOption(args, "--input");
            var outputPath = ReadOption(args, "--output");
            var appliedPath = ReadOptionalOption(args, "--applied");
            var ledgerPath = ReadOptionalOption(args, "--ledger");
            var apply = HasFlag(args, "--apply");
            var applyMarten = HasFlag(args, "--apply-marten");
            var connectionString = ReadOptionalOption(args, "--connection");
            if (apply && ledgerPath is null)
            {
                throw new ArgumentException("'--ledger' is required when '--apply' is specified.");
            }

            if (applyMarten && ledgerPath is null)
            {
                throw new ArgumentException("'--ledger' is required when '--apply-marten' is specified.");
            }

            if (applyMarten && string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("'--connection' is required when '--apply-marten' is specified.");
            }

            if (apply && applyMarten)
            {
                throw new ArgumentException("Use either '--apply' or '--apply-marten', not both.");
            }

            if ((apply || applyMarten) && appliedPath is not null)
            {
                throw new ArgumentException("Use either '--applied' for dry-run or '--ledger --apply' for ledger application, not both.");
            }

            var input = JsonSerializer.Deserialize<LegacyExport>(
                await File.ReadAllTextAsync(inputPath).ConfigureAwait(false),
                JsonOptions) ?? throw new InvalidOperationException("The migration input is empty.");
            MigrationLedger? ledger = null;
            if (ledgerPath is not null)
            {
                ledger = MigrationLedgerStore.Load(
                    ledgerPath,
                    Goal2MigrationPlanner.CurrentToolVersion,
                    Goal2MigrationPlanner.SupportedInputSchemaVersion,
                    JsonOptions);
            }

            var applied = ledger?.Entries
                .Select(entry => entry.SourceReference)
                .ToHashSet(StringComparer.Ordinal)
                ?? (appliedPath is null
                    ? null
                    : JsonSerializer.Deserialize<HashSet<string>>(
                        await File.ReadAllTextAsync(appliedPath).ConfigureAwait(false),
                        JsonOptions));
            var plan = Goal2MigrationPlanner.Plan(input, applied);

            if (!apply && !applyMarten)
            {
                await File.WriteAllTextAsync(
                    outputPath,
                    JsonSerializer.Serialize(plan, JsonOptions)).ConfigureAwait(false);
                await Console.Out.WriteLineAsync(
                    $"GOAL2 migration dry-run wrote {plan.Operations.Count} operations to '{outputPath}'.")
                    .ConfigureAwait(false);
                return 0;
            }

            MigrationBusinessApplyReport? businessReport = null;
            if (applyMarten)
            {
                businessReport = await MartenProjectMigrationApplier.ApplyAsync(
                    plan,
                    input,
                    connectionString!,
                    CancellationToken.None).ConfigureAwait(false);
            }

            var (updatedLedger, report) = MigrationLedgerStore.Apply(
                plan,
                ledger!,
                DateTimeOffset.UtcNow);
            MigrationLedgerStore.SaveAtomic(ledgerPath!, updatedLedger, JsonOptions);
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(new MigrationApplyOutput(plan, report, businessReport), JsonOptions))
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                applyMarten
                    ? $"GOAL2 Marten migration applied: appended {businessReport!.AppendedEventCount} business events, skipped {businessReport.SkippedEventCount}; ledger appended {report.AppendCount}, skipped {report.SkipCount}. Report: '{outputPath}'."
                    : $"GOAL2 migration ledger applied: appended {report.AppendCount}, skipped {report.SkipCount}, total ledger entries {report.LedgerEntriesAfter}. Report: '{outputPath}'.")
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"GOAL2 migration failed: {exception.Message}")
                .ConfigureAwait(false);
            return 2;
        }
    }

    private static string ReadOption(string[] args, string name)
    {
        var value = ReadOptionalOption(args, name);
        if (value is null)
        {
            throw new ArgumentException($"Missing required option '{name}'.");
        }

        return value;
    }

    private static string? ReadOptionalOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                throw new ArgumentException($"Missing value for option '{name}'.");
            }

            return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.Ordinal));
}
