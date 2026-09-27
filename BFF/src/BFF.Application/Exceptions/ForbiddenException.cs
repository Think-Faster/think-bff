namespace BFF.Application.Exceptions;

public sealed class ForbiddenException : Exception
{
    public string ErrorCode { get; }

    public ForbiddenException(string message, string errorCode = "permission_denied") : base(message)
    {
        ErrorCode = errorCode;
    }
}
