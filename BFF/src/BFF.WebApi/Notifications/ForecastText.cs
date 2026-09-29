using System.Globalization;
using System.Text;
using System.Text.Json;
using BFF.Models.Enums;

namespace BFF.WebApi.Notifications;

/// <summary>
/// Текст карточки прогноза из блока типа (ML/INTEGRATION.md §2.3): описание словами и рекомендация по
/// вариантам. score — место часа в распределении парка, не вероятность; вероятность — confidence.
/// </summary>
public static class ForecastText
{
    // Сборка с InvariantGlobalization (Directory.Build.props): культуры ru-RU нет — только десятичная запятая.
    private static readonly NumberFormatInfo Ru = new() { NumberDecimalSeparator = "," };

    public static string Describe(double score, double threshold, double? confidence, int since, JsonElement block)
    {
        var lines = new List<string>
        {
            confidence is { } c
                ? $"Вероятность события в ближайшие сутки — {Percent(c)}: так часто подтверждались тревоги с такой оценкой."
                : "Вероятность не посчитана: для этого типа у модели нет калибровки.",
            string.Create(Ru,
                $"Оценка часа выше, чем у {score * 100:0.#}% часов проверки модели; порог тревоги сейчас — {threshold * 100:0.#}%."),
            since > 1 ? $"Тревога держится {Hours(since)}." : "Тревога появилась в этот час.",
        };

        if (block.TryGetProperty("silent", out var silent) && silent.ValueKind == JsonValueKind.Array
            && silent.GetArrayLength() > 0)
        {
            var families = silent.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => SensorFamilies.Label(x.GetString()!));
            lines.Add($"На объекте молчат датчики: {string.Join(", ", families)} — вероятность снижена с учётом молчания.");
        }

        if (Number(block, "stale_hours") is { } stale and > 0)
        {
            lines.Add($"По всему парку нет событий этого типа {Hours((int)Math.Round(stale))} — поток данных может отставать.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>Рекомендация модели (recommend.py) текстом: основные меры (вариант A) по порядку, другая
    /// гипотеза причины (B), минимальная мера без людей (C), выезд, ТО и заметки. Уходит и в описание заявки.</summary>
    public static string? Recommendation(JsonElement rec)
    {
        if (rec.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var now = Items(rec, "now");
        var sections = new List<string>();
        AddSection(sections, "Сделать сейчас:", now.Where(m => Variant(m) is "A" or null).ToList(), numbered: true);
        AddSection(sections, "Если причина другая:", now.Where(m => Variant(m) == "B").ToList());
        AddSection(sections, "Если на основную меру нет людей:", now.Where(m => Variant(m) == "C").ToList());

        if (rec.TryGetProperty("visit", out var visit) && visit.ValueKind == JsonValueKind.Object
            && Visit(visit) is { } visitLine)
        {
            sections.Add(visitLine);
        }

        AddSection(sections, "В план ТО:", Items(rec, "maintenance"));
        AddSection(sections, "Учесть:", Items(rec, "notes").Concat(now.Where(m => Variant(m) == "+")).ToList());

        return sections.Count > 0 ? string.Join("\n\n", sections) : null;
    }

    private static void AddSection(List<string> sections, string title, IReadOnlyList<JsonElement> items, bool numbered = false)
    {
        var lines = items.Select(Step).OfType<string>().Distinct().ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var text = new StringBuilder(title);
        for (var i = 0; i < lines.Count; i++)
        {
            text.Append('\n').Append(numbered ? $"{i + 1}. " : "• ").Append(lines[i]);
        }

        sections.Add(text.ToString());
    }

    private static string? Step(JsonElement step)
    {
        if (Text(step, "мера") is not { Length: > 0 } measure)
        {
            return null;
        }

        var tail = new List<string>();
        if (Text(step, "исполнитель") is { Length: > 0 } who)
        {
            tail.Add(who);
        }

        if (Number(step, "срок_ч") is { } due and > 0)
        {
            tail.Add($"в течение {Hours((int)due)}");
        }

        return tail.Count > 0 ? $"{measure.TrimEnd('.')} — {string.Join(", ", tail)}" : measure;
    }

    private static string? Visit(JsonElement visit)
    {
        var crew = Text(visit, "состав");
        var people = Number(visit, "людей") ?? 0;
        if (crew is null || crew == "без выезда" || people <= 0)
        {
            return "Выезд: не нужен — меры выполняются без выезда.";
        }

        var text = new StringBuilder("Выезд: ").Append(crew);
        text.Append(people == 1 ? " (1 человек)" : $" из {people:0} человек");
        if (Number(visit, "срок_ч") is { } due and > 0)
        {
            text.Append(", в течение ").Append(Hours((int)due));
        }

        if (visit.TryGetProperty("после_проверки", out var after) && after.ValueKind == JsonValueKind.True)
        {
            text.Append(" — только если проверка без выезда не сняла тревогу");
        }

        return text.Append('.').ToString();
    }

    private static IReadOnlyList<JsonElement> Items(JsonElement rec, string name) =>
        rec.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList()
            : Array.Empty<JsonElement>();

    private static string? Variant(JsonElement step) => Text(step, "вариант");

    private static string Percent(double p) => p < 0.01 ? "меньше 1%" : string.Create(Ru, $"{p * 100:0}%");

    /// <summary>Часы словами: до двух суток — в часах, дальше — в сутках.</summary>
    public static string Hours(int hours) => hours < 48 ? $"{hours} ч" : $"{hours / 24} сут";

    private static double? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
