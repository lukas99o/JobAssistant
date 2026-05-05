using System.Text.Json;

namespace JobAssistant.Core.Models;

public sealed record JobListing
{
    public string Id { get; init; } = string.Empty;

    public string Headline { get; init; } = string.Empty;

    public string EmployerName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string CompanyDesc { get; init; } = string.Empty;

    public IReadOnlyList<string> CompanyKeywords { get; init; } = Array.Empty<string>();

    public string? ApplicationUrl { get; init; }

    public string? ApplicationEmail { get; init; }

    public string? ApplicationInfo { get; init; }

    public string? WorkplaceCity { get; init; }

    public string? PublishedDate { get; init; }

    public string? LastApplyDate { get; init; }

    public string ApplicationMethod => !string.IsNullOrWhiteSpace(ApplicationUrl)
        ? "external"
        : !string.IsNullOrWhiteSpace(ApplicationEmail)
            ? "email"
            : "none";

    public string Summary => $"{Headline} at {EmployerName}";

    public static JobListing FromApiResponse(JsonElement data)
    {
        var applicationDetails = GetObject(data, "application_details");
        var employer = GetObject(data, "employer");
        var workplace = GetObject(data, "workplace_address");

        string description = string.Empty;
        if (data.TryGetProperty("description", out var descriptionElement))
        {
            description = descriptionElement.ValueKind switch
            {
                JsonValueKind.Object => GetString(descriptionElement, "text"),
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => descriptionElement.ToString() ?? string.Empty,
            };
        }

        return new JobListing
        {
            Id = GetStringOrDefault(data, "id"),
            Headline = GetStringOrDefault(data, "headline"),
            EmployerName = GetStringOrDefault(employer, "name", "Unknown"),
            Description = description,
            CompanyDesc = string.Empty,
            CompanyKeywords = Array.Empty<string>(),
            ApplicationUrl = GetOptionalString(applicationDetails, "url"),
            ApplicationEmail = GetOptionalString(applicationDetails, "email"),
            ApplicationInfo = GetOptionalString(applicationDetails, "information"),
            WorkplaceCity = GetOptionalString(workplace, "city") ?? GetOptionalString(workplace, "municipality"),
            PublishedDate = GetOptionalString(data, "publication_date"),
            LastApplyDate = GetOptionalString(data, "last_publication_date"),
        };
    }

    private static JsonElement GetObject(JsonElement data, string propertyName)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        return default;
    }

    private static string GetStringOrDefault(JsonElement data, string propertyName, string defaultValue = "")
    {
        var value = GetOptionalString(data, propertyName);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string GetString(JsonElement data, string propertyName)
    {
        return GetOptionalString(data, propertyName) ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement data, string propertyName)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var value = property.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}