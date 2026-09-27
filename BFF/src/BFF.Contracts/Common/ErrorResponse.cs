namespace BFF.Contracts.Common;

public sealed class ErrorResponse
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public object? Details { get; init; }
}
