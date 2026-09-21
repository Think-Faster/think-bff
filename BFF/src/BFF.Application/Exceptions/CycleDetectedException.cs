namespace BFF.Application.Exceptions;

public sealed class CycleDetectedException : Exception
{
    public string ErrorCode => "cycle_detected";

    public CycleDetectedException(string message) : base(message)
    {
    }
}
