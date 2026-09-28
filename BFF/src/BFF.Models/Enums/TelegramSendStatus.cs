namespace BFF.Models.Enums;

public enum TelegramSendStatus
{
    Sent,
    RateLimited,
    UserNotFound,
    NoTelegramOnFile,
    NotLinked,
    Failed
}
