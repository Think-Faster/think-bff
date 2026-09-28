namespace BFF.Models.Constants;

/// <summary>
/// Живость эпизода по факту (ML/INTEGRATION.md §13.11): модель шлёт живой эпизод каждый час с last_at, и
/// эпизод закрывается, когда у него час нет сработок (EPISODE_GAP). Пока last_at не старше окна — эпизод
/// живой и задаёт статус объекта.
/// </summary>
public static class FactAlertLiveness
{
    /// <summary>EPISODE_GAP модели (1 ч) + такт канала «по факту» (1 ч).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(2);

    public static DateTimeOffset LiveSince(DateTimeOffset now) => now - Window;
}
