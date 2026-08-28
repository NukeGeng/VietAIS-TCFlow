using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public sealed record CodexAppServerOptions(
    string ExecutablePath,
    string IsolatedWorkingDirectory,
    string? Model = null,
    string ClientName = "vietais_tcflow",
    string ClientTitle = "VietAIS TCFlow",
    string ClientVersion = "0.1.0");

public sealed class CodexAppServerProtocolException(string message) : InvalidOperationException(message);

public sealed class CodexAppServerProcessClient : ICodexAppServerClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(AnalysisJson.Options)
    {
        WriteIndented = false
    };

    private readonly CodexAppServerOptions _options;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SemaphoreSlim _turnGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly Channel<JsonElement> _notifications = Channel.CreateUnbounded<JsonElement>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _process;
    private Task? _readLoop;
    private Task? _errorLoop;
    private long _requestId;
    private bool _initialized;
    private bool _disposed;

    public CodexAppServerProcessClient(CodexAppServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            throw new ArgumentException("Codex executable path is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.IsolatedWorkingDirectory))
        {
            throw new ArgumentException("An isolated Codex working directory is required.", nameof(options));
        }

        _options = options with
        {
            ExecutablePath = options.ExecutablePath.Trim(),
            IsolatedWorkingDirectory = Path.GetFullPath(options.IsolatedWorkingDirectory)
        };
    }

    public async Task<CodexAccountState> ReadAccountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var result = await SendRequestAsync(
            "account/read",
            new { refreshToken = false },
            cancellationToken);
        var accountType = result.TryGetProperty("account", out var account) &&
            account.ValueKind == JsonValueKind.Object &&
            account.TryGetProperty("type", out var type)
                ? type.GetString()
                : null;
        var requiresOpenAiAuth = result.TryGetProperty("requiresOpenaiAuth", out var required) &&
            required.ValueKind == JsonValueKind.True;
        return new CodexAccountState(accountType, requiresOpenAiAuth);
    }

    public async Task<string> RunStructuredTurnAsync(
        string prompt,
        JsonElement outputSchema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A reasoning prompt is required.", nameof(prompt));
        }

        if (outputSchema.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("A JSON object output schema is required.", nameof(outputSchema));
        }

        await EnsureInitializedAsync(cancellationToken);
        await _turnGate.WaitAsync(cancellationToken);
        try
        {
            var threadParameters = new Dictionary<string, object?>
            {
                ["cwd"] = _options.IsolatedWorkingDirectory,
                ["approvalPolicy"] = "never",
                ["permissions"] = ":read-only",
                ["runtimeWorkspaceRoots"] = new[] { _options.IsolatedWorkingDirectory },
                ["serviceName"] = _options.ClientName,
                ["model"] = _options.Model
            };
            var threadResult = await SendRequestAsync("thread/start", threadParameters, cancellationToken);
            var threadId = RequiredString(threadResult, "thread", "id");
            var turnParameters = new Dictionary<string, object?>
            {
                ["threadId"] = threadId,
                ["input"] = new[] { new { type = "text", text = prompt } },
                ["cwd"] = _options.IsolatedWorkingDirectory,
                ["approvalPolicy"] = "never",
                ["permissions"] = ":read-only",
                ["runtimeWorkspaceRoots"] = new[] { _options.IsolatedWorkingDirectory },
                ["outputSchema"] = outputSchema.Clone(),
                ["model"] = _options.Model
            };
            var turnResult = await SendRequestAsync("turn/start", turnParameters, cancellationToken);
            var turnId = RequiredString(turnResult, "turn", "id");
            return await ReadTurnResultAsync(threadId, turnId, cancellationToken);
        }
        finally
        {
            _turnGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        if (_process is { HasExited: false } process)
        {
            process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }

        if (_readLoop is not null)
        {
            await IgnoreCancellationAsync(_readLoop);
        }

        if (_errorLoop is not null)
        {
            await IgnoreCancellationAsync(_errorLoop);
        }

        _process?.Dispose();
        _lifetime.Dispose();
        _startGate.Dispose();
        _writeGate.Dispose();
        _turnGate.Dispose();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_options.IsolatedWorkingDirectory);
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                WorkingDirectory = _options.IsolatedWorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("app-server");
            startInfo.ArgumentList.Add("--listen");
            startInfo.ArgumentList.Add("stdio://");
            _process = Process.Start(startInfo) ??
                throw new CodexAppServerProtocolException("Codex App Server process did not start.");
            _readLoop = ReadMessagesAsync(_process, _lifetime.Token);
            _errorLoop = DrainStandardErrorAsync(_process, _lifetime.Token);
            await SendRequestAsync(
                "initialize",
                new
                {
                    clientInfo = new
                    {
                        name = _options.ClientName,
                        title = _options.ClientTitle,
                        version = _options.ClientVersion
                    },
                    capabilities = new { experimentalApi = true }
                },
                cancellationToken);
            await SendNotificationAsync("initialized", new { }, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
        {
            throw new CodexAppServerProtocolException($"Duplicate App Server request id '{id}'.");
        }

        try
        {
            await SendMessageAsync(new { method, id, @params = parameters }, cancellationToken);
            return await completion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private Task SendNotificationAsync(string method, object parameters, CancellationToken cancellationToken) =>
        SendMessageAsync(new { method, @params = parameters }, cancellationToken);

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        var process = _process ?? throw new CodexAppServerProtocolException("Codex App Server is not running.");
        var line = JsonSerializer.Serialize(message, WireJsonOptions);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ReadMessagesAsync(Process process, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                using var document = JsonDocument.Parse(line);
                var message = document.RootElement.Clone();
                if (message.TryGetProperty("method", out _) && message.TryGetProperty("id", out var serverRequestId))
                {
                    await SendMessageAsync(
                        new
                        {
                            id = serverRequestId.Clone(),
                            error = new { code = -32601, message = "Server requests are disabled for bounded reasoning." }
                        },
                        cancellationToken);
                    continue;
                }

                if (message.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var id))
                {
                    CompleteRequest(id, message);
                    continue;
                }

                if (message.TryGetProperty("method", out _))
                {
                    await _notifications.Writer.WriteAsync(message, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            var terminal = failure ?? new CodexAppServerProtocolException("Codex App Server output stream closed.");
            foreach (var pending in _pending.Values)
            {
                pending.TrySetException(terminal);
            }

            _notifications.Writer.TryComplete(failure);
        }
    }

    private static async Task DrainStandardErrorAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                await process.StandardError.ReadLineAsync(cancellationToken) is not null)
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void CompleteRequest(long id, JsonElement message)
    {
        if (!_pending.TryGetValue(id, out var completion))
        {
            return;
        }

        if (message.TryGetProperty("error", out var error))
        {
            var errorMessage = error.TryGetProperty("message", out var text)
                ? text.GetString()
                : "Unknown App Server request error.";
            completion.TrySetException(new CodexAppServerProtocolException(errorMessage ?? "Unknown App Server error."));
            return;
        }

        if (!message.TryGetProperty("result", out var result))
        {
            completion.TrySetException(new CodexAppServerProtocolException("App Server response has no result."));
            return;
        }

        completion.TrySetResult(result.Clone());
    }

    private async Task<string> ReadTurnResultAsync(
        string threadId,
        string turnId,
        CancellationToken cancellationToken)
    {
        string? finalOutput = null;
        while (true)
        {
            var notification = await _notifications.Reader.ReadAsync(cancellationToken);
            var method = notification.GetProperty("method").GetString();
            if (!notification.TryGetProperty("params", out var parameters) ||
                !MatchesTurn(parameters, threadId, turnId))
            {
                continue;
            }

            if (method == "item/completed" &&
                parameters.TryGetProperty("item", out var item) &&
                item.TryGetProperty("type", out var itemType) &&
                itemType.GetString() == "agentMessage" &&
                item.TryGetProperty("text", out var text) &&
                (!item.TryGetProperty("phase", out var phase) || phase.GetString() == "final_answer"))
            {
                finalOutput = text.GetString();
            }

            if (method != "turn/completed" || !parameters.TryGetProperty("turn", out var turn))
            {
                continue;
            }

            var status = turn.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            if (status != "completed")
            {
                var message = turn.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var errorMessage)
                        ? errorMessage.GetString()
                        : null;
                throw new CodexAppServerProtocolException(
                    message ?? $"Codex reasoning turn ended with status '{status ?? "unknown"}'.");
            }

            return !string.IsNullOrWhiteSpace(finalOutput)
                ? finalOutput
                : throw new CodexAppServerProtocolException("Codex reasoning turn completed without a final answer.");
        }
    }

    private static bool MatchesTurn(JsonElement parameters, string threadId, string turnId)
    {
        var candidateThreadId = parameters.TryGetProperty("threadId", out var thread)
            ? thread.GetString()
            : null;
        var candidateTurnId = parameters.TryGetProperty("turnId", out var directTurn)
            ? directTurn.GetString()
            : parameters.TryGetProperty("turn", out var turn) && turn.TryGetProperty("id", out var nestedTurn)
                ? nestedTurn.GetString()
                : null;
        return string.Equals(candidateThreadId, threadId, StringComparison.Ordinal) &&
            string.Equals(candidateTurnId, turnId, StringComparison.Ordinal);
    }

    private static string RequiredString(JsonElement element, string objectName, string propertyName)
    {
        if (element.TryGetProperty(objectName, out var nested) &&
            nested.TryGetProperty(propertyName, out var value) &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new CodexAppServerProtocolException(
            $"App Server response is missing '{objectName}.{propertyName}'.");
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (CodexAppServerProtocolException)
        {
        }
    }
}
