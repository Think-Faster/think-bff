namespace BFF.Models.Enums;

public enum PredictionType
{
    Fire = 1,
    Gas = 2,
    Flood = 3,
    EquipmentFailure = 4,
    SensorFailure = 5,
    Intrusion = 6,

    /// <summary>Аномальная температура по медиане канала — только по факту, прогноза нет.</summary>
    Temperature = 7,

    /// <summary>Слепота объекта: нет связи или питания — только по факту, прогноза нет.</summary>
    Blind = 8,
}
