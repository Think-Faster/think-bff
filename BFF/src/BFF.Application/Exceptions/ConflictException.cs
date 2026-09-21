namespace BFF.Application.Exceptions;

/// <summary>
/// Generic 409 conflict, e.g. duplicate group/resource code or a mutation forbidden on a system group.
/// Not part of the original exception list in section 3 of the spec; added because several endpoints in
/// sections 10.1/10.2 require a distinct 409 response beyond the cycle-detection case. See docs/DECISIONS.md.
/// </summary>
public sealed class ConflictException : Exception
{
    public string ErrorCode { get; }

    public ConflictException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}
