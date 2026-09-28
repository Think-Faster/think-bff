namespace BFF.Models.Enums;

/// <summary>Группа события (ML/INTEGRATION.md §13.11): авария даёт объекту ALARM, инцидент — нет.</summary>
public enum AlertGroup
{
    Accident = 1,
    Incident = 2,
}
