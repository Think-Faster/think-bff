namespace BFF.Models.Constants;

/// <summary>Predefined resource codes, also seeded by scripts/001_seed_initial_data.sql.</summary>
public static class ResourceCodes
{
    public const string Users = "users";
    public const string Groups = "groups";
    public const string Permissions = "permissions";

    // D1 — topology: MonitoringObject, Picket, MapLayer.
    public const string Objects = "objects";
    // D1 — Sensor, SensorLink (dictionary only — live state stays with tf-funnel).
    public const string Sensors = "sensors";
    // D3 — Prediction (+ factors/evidence/decisions), FactAlert.
    public const string Predictions = "predictions";
    // D4 — WorkTask and its sub-resources (predictions attached, assignments, reports, returns).
    public const string Tasks = "tasks";
    // D4 — Incident.
    public const string Incidents = "incidents";
    // D6 — ScheduleEntry, nested under users.
    public const string Schedule = "schedule";
    // D6 — AssignedObject, nested under users.
    public const string AssignedObjects = "assigned_objects";
    // D6 — EngineerProfile, Brigade.
    public const string Engineers = "engineers";
    // D6 — UserActivity (read-only; written internally by TokenAuthenticationMiddleware, not via API).
    public const string Presence = "presence";
    // D8 — ModelVersion (control side), Coefficient, RetrainJob, IgnoredRange — bundled per the source
    // doc's own framing ("пять блоков, все под аудит" — one admin-settings permission group).
    public const string ModelSettings = "model_settings";
}
