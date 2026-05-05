using System.Text.Json.Serialization;

namespace JobAssistant.Core.Models;

public sealed record JobHistoryRecord
{
    [JsonPropertyName("job_id")]
    public string JobId { get; init; } = string.Empty;

    [JsonPropertyName("company_name")]
    public string CompanyName { get; init; } = string.Empty;

    [JsonPropertyName("headline")]
    public string Headline { get; init; } = string.Empty;

    [JsonPropertyName("company_desc")]
    public string CompanyDesc { get; init; } = string.Empty;

    [JsonPropertyName("company_keywords")]
    public IReadOnlyList<string> CompanyKeywords { get; init; } = Array.Empty<string>();

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    [JsonPropertyName("last_search_date")]
    public string LastSearchDate { get; init; } = string.Empty;

    [JsonPropertyName("search_query")]
    public string SearchQuery { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}