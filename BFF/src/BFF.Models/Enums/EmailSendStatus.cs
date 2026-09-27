namespace BFF.Models.Enums;

public enum EmailSendStatus
{
    Sent,
    RateLimited,
    UserNotFound,
    NoEmailOnFile,
    InvalidEmail,
    Failed
}
