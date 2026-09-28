using System.Text.RegularExpressions;

namespace BFF.Models.Constants;

/// <summary>
/// Имя пользователя Telegram (users.telegram): хранится в нижнем регистре и без @. Те же правила, что у
/// tf-tg (think-infra/telegram/app/links.py): человек сам подключает бота — «Старт» у бота, и tf-tg
/// запоминает `tg:user:&lt;username&gt;` = chat_id; BFF шлёт по username в `to.usernames`.
/// </summary>
public static class TelegramUsername
{
    private static readonly Regex Pattern = new(@"^@?([A-Za-z][A-Za-z0-9_]{4,31})$", RegexOptions.Compiled);

    public const int MaxLength = 32;

    /// <summary>«@Ivan_Petrov» → «ivan_petrov»; пусто — null; не похоже на username — null.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Pattern.Match(value.Trim());
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    /// <summary>Пусто — допустимо (Telegram не указан); иначе — по правилам Telegram.</summary>
    public static bool IsValidOrEmpty(string? value) => string.IsNullOrWhiteSpace(value) || Normalize(value) is not null;
}
