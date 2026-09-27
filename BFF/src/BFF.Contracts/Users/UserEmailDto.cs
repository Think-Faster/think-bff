namespace BFF.Contracts.Users;

/// <summary>Результат резолва одного запрошенного userId в email — для рассылок (Notifications).
/// Found=false — пользователя с таким Id не существует; Found=true с Email=null — существует, но
/// почта не указана в профиле.</summary>
public sealed class UserEmailDto
{
    public Guid UserId { get; init; }
    public bool Found { get; init; }
    public string? Email { get; init; }
}
