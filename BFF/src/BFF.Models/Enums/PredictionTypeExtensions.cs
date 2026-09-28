namespace BFF.Models.Enums;

/// <summary>Справочник типов (ML/INTEGRATION.md §2.3, §13.11): имя в сообщениях модели, подпись и группа.</summary>
public static class PredictionTypeExtensions
{
    private static readonly Dictionary<string, PredictionType> ByModelName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fire"] = PredictionType.Fire,
        ["gas"] = PredictionType.Gas,
        ["flood"] = PredictionType.Flood,
        ["equipment"] = PredictionType.EquipmentFailure,
        ["sensor"] = PredictionType.SensorFailure,
        ["intrusion"] = PredictionType.Intrusion,
        ["temperature"] = PredictionType.Temperature,
        ["blind"] = PredictionType.Blind,
    };

    /// <summary>Прогнозные типы модели (§2.3): у них рабочие доли, версии и порог; temperature и blind — только факт (§13.11).</summary>
    public static readonly IReadOnlyList<string> ForecastModelNames =
        ["fire", "gas", "flood", "equipment", "sensor", "intrusion"];

    /// <summary>Имя типа в модели (fire, gas, flood, equipment, sensor, intrusion, temperature, blind).</summary>
    public static string ToModelString(this PredictionType type) =>
        ByModelName.FirstOrDefault(p => p.Value == type).Key ?? type.ToString().ToLowerInvariant();

    public static bool TryParseModel(string? value, out PredictionType type) =>
        ByModelName.TryGetValue(value ?? string.Empty, out type);

    public static string Label(this PredictionType type) => type switch
    {
        PredictionType.Fire => "Пожар",
        PredictionType.Gas => "Загазованность",
        PredictionType.Flood => "Подтопление",
        PredictionType.EquipmentFailure => "Отказ оборудования",
        PredictionType.SensorFailure => "Отказ датчика",
        PredictionType.Intrusion => "Проникновение",
        PredictionType.Temperature => "Аномальная температура",
        PredictionType.Blind => "Потеря связи или питания",
        _ => type.ToString(),
    };

    public static AlertGroup Group(this PredictionType type) => type switch
    {
        PredictionType.EquipmentFailure or PredictionType.SensorFailure or PredictionType.Blind => AlertGroup.Incident,
        _ => AlertGroup.Accident,
    };

    public static string Label(this AlertGroup group) => group switch
    {
        AlertGroup.Accident => "Авария",
        _ => "Инцидент",
    };
}
