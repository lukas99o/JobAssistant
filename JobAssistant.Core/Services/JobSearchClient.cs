using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public sealed class JobSearchClient : IDisposable
{
    private readonly string _baseUrl;
    private readonly int _batchSize;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public JobSearchClient(Settings settings, HttpClient? client = null)
    {
        _baseUrl = settings.ApiBaseUrl;
        _batchSize = settings.ApiBatchSize;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsClient = client is null;

        if (!_client.DefaultRequestHeaders.Accept.Any(header => header.MediaType == "application/json"))
        {
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<SearchResultPage> SearchAsync(string query, int offset = 0, int? limit = null, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["q"] = query,
            ["offset"] = offset.ToString(),
            ["limit"] = (limit ?? _batchSize).ToString(),
        };

        using var document = await RequestAsync("/search", parameters, cancellationToken);
        var root = document.RootElement;

        var total = 0;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("total", out var totalElement) && totalElement.ValueKind == JsonValueKind.Object)
        {
            total = TryGetInt32(totalElement, "value");
        }

        var jobs = new List<JobListing>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("hits", out var hitsElement) && hitsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var hit in hitsElement.EnumerateArray())
            {
                jobs.Add(JobListing.FromApiResponse(hit));
            }
        }

        return new SearchResultPage(jobs, total);
    }

    public async Task<JobListing?> GetAdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await RequestAsync($"/ad/{jobId}", null, cancellationToken);
            return JobListing.FromApiResponse(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private async Task<JsonDocument> RequestAsync(string path, IReadOnlyDictionary<string, string?>? parameters, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var url = BuildUrl(_baseUrl, path, parameters);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _client.SendAsync(request, cancellationToken);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var waitSeconds = (int)Math.Pow(2, attempt + 1);
                    global::System.Console.WriteLine($"  Rate limited. Waiting {waitSeconds}s...");
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (HttpRequestException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        return JsonDocument.Parse("{}");
    }

    private static string BuildUrl(string baseUrl, string path, IReadOnlyDictionary<string, string?>? parameters)
    {
        var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        var baseUri = new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), path.TrimStart('/'));
        var builder = new UriBuilder(baseUri);

        if (parameters is null || parameters.Count == 0)
        {
            return builder.Uri.ToString();
        }

        var query = string.Join(
            "&",
            parameters
                .Where(pair => pair.Value is not null)
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        builder.Query = query;
        return builder.Uri.ToString();
    }

    private static int TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(property.ToString(), out var parsed) ? parsed : 0;
    }
}