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

    public static int Main(string[] args)
    {
        try
        {
            var inputPath = ReadOption(args, "--input");
            var outputPath = ReadOption(args, "--output");
            var appliedPath = ReadOptionalOption(args, "--applied");
            var input = JsonSerializer.Deserialize<LegacyExport>(
                File.ReadAllText(inputPath),
                JsonOptions) ?? throw new InvalidOperationException("The migration input is empty.");
            var applied = appliedPath is null
                ? null
                : JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(appliedPath), JsonOptions);
            var plan = Goal2MigrationPlanner.Plan(input, applied);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(plan, JsonOptions));
            Console.WriteLine($"GOAL2 migration dry-run wrote {plan.Operations.Count} operations to '{outputPath}'.");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Migration dry-run failed: {exception.Message}");
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
}
