using System.Text.Json;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public sealed record CodexAccountState(string? AccountType, bool RequiresOpenAiAuth);

public interface ICodexAppServerClient
{
    Task<CodexAccountState> ReadAccountAsync(CancellationToken cancellationToken = default);

    Task<string> RunStructuredTurnAsync(
        string prompt,
        JsonElement outputSchema,
        CancellationToken cancellationToken = default);
}

public sealed class CodexAuthenticationRequiredException(string message) : InvalidOperationException(message);

public sealed class CodexAppServerReasoningProvider(ICodexAppServerClient client) : IAiReasoningProvider
{
    private static readonly JsonElement ImpactOutputSchema = CreateOutputSchema();

    public async Task<AiImpactReasoningResult> AnalyzeImpactAsync(
        AiReasoningContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var account = await client.ReadAccountAsync(cancellationToken);
        if (account.RequiresOpenAiAuth && string.IsNullOrWhiteSpace(account.AccountType))
        {
            throw new CodexAuthenticationRequiredException(
                "Codex App Server requires a managed account login before AI reasoning can run.");
        }

        var prompt = BuildPrompt(context);
        var output = await client.RunStructuredTurnAsync(prompt, ImpactOutputSchema, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<AiImpactReasoningResult>(output, AnalysisJson.Options)
                ?? throw new InvalidOperationException("Codex returned an empty structured impact result.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Codex returned an impact result that does not match the required structured schema.",
                exception);
        }
    }

    private static string BuildPrompt(AiReasoningContext context) => $$"""
        You are the bounded reasoning stage of a source-aware engineering planner.
        Analyze only the supplied targeted repository context. Do not inspect files, run commands,
        call tools, or invent artifact/evidence identifiers. Every returned identifier must already
        exist in the supplied context. Confirmed deterministic facts remain source truth; your
        semantic conclusions must be inferred or proposed and must include calibrated confidence.
        Return only JSON matching the provided output schema.

        Targeted context:
        {{JsonSerializer.Serialize(context, AnalysisJson.Options)}}
        """;

    private static JsonElement CreateOutputSchema()
    {
        using var document = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "summary": { "type": "string", "minLength": 1 },
                "severity": {
                  "type": "string",
                  "enum": ["none", "low", "medium", "high", "critical"]
                },
                "evidenceLevel": {
                  "type": "string",
                  "enum": ["inferred", "proposed"]
                },
                "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                "evidenceIds": {
                  "type": "array",
                  "items": { "type": "string" }
                },
                "tasks": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "title": { "type": "string", "minLength": 1 },
                      "description": { "type": ["string", "null"] },
                      "targetComponent": {
                        "type": "string",
                        "enum": ["frontend", "backend", "shared"]
                      },
                      "evidenceLevel": {
                        "type": "string",
                        "enum": ["inferred", "proposed"]
                      },
                      "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                      "artifactIds": {
                        "type": "array",
                        "items": { "type": "string" }
                      },
                      "evidenceIds": {
                        "type": "array",
                        "items": { "type": "string" }
                      },
                      "requirements": {
                        "type": "array",
                        "items": { "type": "string" }
                      }
                    },
                    "required": [
                      "title",
                      "description",
                      "targetComponent",
                      "evidenceLevel",
                      "confidence",
                      "artifactIds",
                      "evidenceIds",
                      "requirements"
                    ],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["summary", "severity", "evidenceLevel", "confidence", "evidenceIds", "tasks"],
              "additionalProperties": false
            }
            """);
        return document.RootElement.Clone();
    }
}
