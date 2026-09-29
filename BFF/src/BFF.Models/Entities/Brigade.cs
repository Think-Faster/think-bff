namespace BFF.Models.Entities;

public sealed class Brigade
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Эксплуатационное подразделение (участок, район); бригадир — поле бригады, а не роль человека.</summary>
    public string? Unit { get; set; }
    public Guid? LeaderId { get; set; }
}
