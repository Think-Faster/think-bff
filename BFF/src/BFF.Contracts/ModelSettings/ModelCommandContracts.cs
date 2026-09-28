namespace BFF.Contracts.ModelSettings;

// Команды админ-панели модели (Think-Faster ML/INTEGRATION.md §13.3): BFF проверяет право и публикует
// в tf.model.commands, состояние хранит и отдаёт сама модель (/api/ml/status). Тип — имя в модели
// (fire, gas, flood, equipment, sensor, intrusion), как в /status.

/// <summary>model.switch: какая из собранных версий считает тип; <c>VersionId</c> null — основная выгрузка.</summary>
public sealed class SwitchModelVersionRequest
{
    public string Type { get; init; } = string.Empty;
    public int? VersionId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>settings.operating: полный снимок рабочих долей всех шести типов, <c>Version</c> — следующий номер.</summary>
public sealed class OperatingSettingsRequest
{
    public int Version { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Dictionary<string, OperatingTypeSettings> Types { get; init; } = new();
}

public sealed class OperatingTypeSettings
{
    public double Share { get; init; }
    public double? RejectK { get; init; }
}

/// <summary>settings.gaps: вся таблица игнорируемых периодов (брак данных), <c>Version</c> — следующий номер.</summary>
public sealed class IgnoredPeriodsRequest
{
    public int Version { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<IgnoredPeriodRow> Rows { get; init; } = new();
}

/// <summary>Период по московскому времени, «ГГГГ-ММ-ДД ЧЧ:ММ», как в таблице модели.</summary>
public sealed class IgnoredPeriodRow
{
    public string A { get; init; } = string.Empty;
    public string B { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

/// <summary>202: команда в очереди модели. Итог — в /api/ml/status (номер версии) и в аудите модели.</summary>
public sealed class ModelCommandAcceptedResponse
{
    public Guid CommandId { get; init; }
    public string Kind { get; init; } = string.Empty;
}
