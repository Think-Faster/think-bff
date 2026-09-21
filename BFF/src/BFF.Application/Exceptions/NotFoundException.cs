namespace BFF.Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public string ErrorCode => "not_found";

    public NotFoundException(string message) : base(message)
    {
    }
}
