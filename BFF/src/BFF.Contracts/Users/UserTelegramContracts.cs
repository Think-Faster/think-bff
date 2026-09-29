namespace BFF.Contracts.Users;

/// <summary>PUT /users/me/telegram — своё имя в Telegram; пусто или null — отвязать.</summary>
public sealed class UpdateMyTelegramRequest
{
    public string? Username { get; init; }
}

/// <summary>
/// GET/PUT /users/me/telegram. Linked — человек нажал «Старт» у бота под этим именем (tf-tg хранит связь
/// в Redis `tg:user:&lt;username&gt;`), null — неизвестно (Redis недоступен). Bot — имя бота без @ для ссылки
/// t.me/&lt;bot&gt;, null — tf-tg ещё не запускался или Redis недоступен.
/// </summary>
public sealed class TelegramStatusDto
{
    public string? Username { get; init; }
    public bool? Linked { get; init; }
    public string? Bot { get; init; }
}

/// <summary>Результат резолва userId в Telegram для рассылок — как UserEmailDto у почты.</summary>
public sealed class UserTelegramDto
{
    public Guid UserId { get; init; }
    public bool Found { get; init; }
    public string? Telegram { get; init; }
}
