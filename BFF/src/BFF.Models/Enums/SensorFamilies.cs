namespace BFF.Models.Enums;

/// <summary>Семейства датчиков модели (ML/INTEGRATION.md §2.3, поле silent; ML/service/snapshot.py FAMILIES):
/// код в сообщении модели → подпись для карточки.</summary>
public static class SensorFamilies
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smoke"] = "дымовые",
        ["heat"] = "тепловые",
        ["temp"] = "температуры",
        ["gas"] = "газовые",
        ["flood"] = "затопления",
        ["pump"] = "насосы",
        ["phase"] = "фазы питания",
        ["ups"] = "ИБП",
        ["fan"] = "вентиляторы",
        ["door"] = "двери",
        ["motion"] = "движения",
        ["hatch"] = "люки",
    };

    /// <summary>Подпись семейства; незнакомый код — как есть.</summary>
    public static string Label(string code) => Labels.GetValueOrDefault(code, code);
}
