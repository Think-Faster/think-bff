using System.Text.Json;

namespace BFF.WebApi.Extensions;

/// <summary>
/// Middleware writes error bodies directly via JsonSerializer.Serialize, bypassing the MVC pipeline's own
/// JSON options — which default to camelCase. Without sharing that policy here, error responses would
/// come back PascalCase ("Code") while every successful controller response is camelCase ("code"),
/// forcing frontend code to special-case error bodies.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Also covers ErrorResponse.Details, which validation errors populate as a
        // Dictionary<string, string[]> keyed by request property name (e.g. "LastName") — without this,
        // only ErrorResponse's own properties would be camelCased, not the field names inside Details.
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };
}
