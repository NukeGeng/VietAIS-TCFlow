namespace VietAIS.TCFlow.BuildingBlocks.Application.Results;

public sealed record ResultError(string Code, string Message)
{
    public static readonly ResultError None = new(string.Empty, string.Empty);

    public static ResultError Validation(string code, string message) => Create(code, message);

    public static ResultError Conflict(string code, string message) => Create(code, message);

    public static ResultError NotFound(string code, string message) => Create(code, message);

    private static ResultError Create(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ResultError(code.Trim(), message.Trim());
    }
}
