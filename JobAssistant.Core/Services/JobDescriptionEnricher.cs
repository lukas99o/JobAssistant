using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public sealed class JobDescriptionEnricher : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex KeywordRegex = new(@"(?<![\p{L}\p{N}])[\p{L}\p{N}][\p{L}\p{N}\+#\./-]{1,}(?![\p{L}\p{N}])", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "att", "av", "be", "but", "by", "com", "de", "den", "det", "du", "e", "eller",
        "en", "ett", "for", "fran", "för", "from", "har", "hos", "i", "if", "in", "into", "it", "its", "jobbet", "med", "mer",
        "mig", "nu", "och", "om", "on", "or", "oss", "our", "på", "role", "roll", "som", "the", "this", "till", "to", "vi",
        "vill", "vår", "vårt", "with", "you", "your"
    };

    private readonly Settings _settings;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public JobDescriptionEnricher(Settings settings, HttpClient? client = null)
    {
        _settings = settings;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(settings.OllamaTimeoutSeconds) };
        _ownsClient = client is null;
    }

    public async Task<JobDescriptionAnalysisResult> AnalyzeAsync(string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return JobDescriptionAnalysisResult.Empty;
        }

        if (!_settings.OllamaEnabled)
        {
            return CreateFallbackResult(description) with { WarningMessage = "Ollama enrichment is disabled in settings. Using local extraction." };
        }

        return await AnalyzeRequiredAsync(description, cancellationToken);
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.OllamaEnabled)
        {
            return;
        }

        const string sampleDescription = "Vi söker en erfaren .NET-utvecklare som bygger API:er, arbetar med Azure och samarbetar nära produktteam i en agil miljö.";
        await AnalyzeRequiredAsync(sampleDescription, cancellationToken);
    }

    private async Task<JobDescriptionAnalysisResult> AnalyzeRequiredAsync(string description, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
            {
                Content = new StringContent(BuildRequestBody(description), Encoding.UTF8, "application/json"),
            };

            using var response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OllamaGenerateResponse>(stream, SerializerOptions, cancellationToken);
            var analysis = ParseStructuredResponse(payload?.Response, payload?.Thinking);

            if (analysis is null)
            {
                throw new JobDescriptionEnrichmentException($"Ollama returned an unreadable response for model '{_settings.OllamaModel}'.");
            }

            return analysis with { WarningMessage = null, UsedFallback = false };
        }
        catch (JsonException exception)
        {
            throw new JobDescriptionEnrichmentException(
                $"Ollama returned an unreadable response for model '{_settings.OllamaModel}'.",
                exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new JobDescriptionEnrichmentException(
                $"Ollama enrichment failed at {_settings.OllamaBaseUrl} using model '{_settings.OllamaModel}'. Ensure Ollama is running and the model is available.",
                exception);
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private string BuildEndpoint()
    {
        return $"{_settings.OllamaBaseUrl.TrimEnd('/')}/api/generate";
    }

    private string BuildRequestBody(string description)
    {
        var payload = new
        {
            model = _settings.OllamaModel,
            stream = false,
            think = false,
            format = "json",
            options = new
            {
                temperature = 0.1,
            },
            prompt = BuildPrompt(description),
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildPrompt(string description)
    {
                return string.Join(
                        Environment.NewLine,
                        "You process raw job descriptions for a job-search assistant.",
                        string.Empty,
                        "Return only valid JSON with this exact schema:",
                        "{",
                        "  \"company_desc\": \"string\",",
                        "  \"company_keywords\": [\"string\"]",
                        "}",
                        string.Empty,
                        "Rules:",
                        "- Keep company_desc concise, but preserve the important details.",
                        "- company_desc should usually be 80 to 220 words and must never exceed 500 words.",
                        "- Keep the same language as the input when possible.",
                        "- Include responsibilities, required skills, nice-to-have skills, domain context, seniority, work model, and other material details when present.",
                        "- Use short paragraphs separated by newline characters when helpful.",
                        "- Do not copy long passages verbatim from the source text.",
                        "- company_keywords must contain 8 to 20 relevant keywords or short phrases.",
                        "- Do not include markdown or explanations.",
                        string.Empty,
                        "Job description:",
                        "\"\"\"",
                        description,
                        "\"\"\"");
    }

    private JobDescriptionAnalysisResult CreateFallbackResult(string description)
    {
        return new JobDescriptionAnalysisResult(
            CompanyDesc: SummarizeFallback(description),
            CompanyKeywords: ExtractKeywords(description),
            UsedFallback: true,
            WarningMessage: null);
    }

    private static JobDescriptionAnalysisResult? ParseStructuredResponse(string? responseText, string? thinkingText)
    {
        var json = ExtractJson(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            json = ExtractJson(thinkingText);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var companyDesc = root.TryGetProperty("company_desc", out var descElement)
            ? NormalizeSummary(descElement.ToString())
            : string.Empty;

        var companyKeywords = root.TryGetProperty("company_keywords", out var keywordElement)
            ? NormalizeKeywords(ReadKeywords(keywordElement))
            : Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(companyDesc) && companyKeywords.Count == 0)
        {
            return null;
        }

        return new JobDescriptionAnalysisResult(companyDesc, companyKeywords, UsedFallback: false, WarningMessage: null);
    }

    private static string ExtractJson(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        var trimmed = responseText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[4..].Trim();
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end >= start ? trimmed[start..(end + 1)] : trimmed;
    }

    private static IReadOnlyList<string> ReadKeywords(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element
                .EnumerateArray()
                .Select(item => item.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray()!;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element
                .GetString()!
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        return Array.Empty<string>();
    }

    private static string SummarizeFallback(string description)
    {
        var normalized = NormalizeText(description);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var sentences = Regex.Split(normalized, @"(?<=[\.!\?])\s+")
            .Select(sentence => sentence.Trim())
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToArray();

        if (sentences.Length == 0)
        {
            return normalized;
        }

        var summarySentences = new List<string>();
        var wordCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceWordCount = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (summarySentences.Count > 0 && wordCount + sentenceWordCount > 220)
            {
                break;
            }

            summarySentences.Add(sentence);
            wordCount += sentenceWordCount;

            if (wordCount >= 180)
            {
                break;
            }
        }

        if (summarySentences.Count == 0)
        {
            summarySentences.Add(sentences[0]);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, summarySentences);
    }

    private static string NormalizeSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var normalizedParagraphs = summary
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries)
            .Select(paragraph => NormalizeText(paragraph))
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();

        if (normalizedParagraphs.Length == 0)
        {
            return string.Empty;
        }

        var text = string.Join(Environment.NewLine + Environment.NewLine, normalizedParagraphs);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 500)
        {
            return text;
        }

        return string.Join(' ', words.Take(500)) + "...";
    }

    private static string NormalizeText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : WhitespaceRegex.Replace(text, " ").Trim();
    }

    private static IReadOnlyList<string> ExtractKeywords(string description)
    {
        var candidates = KeywordRegex.Matches(NormalizeText(description));
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var display = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in candidates)
        {
            var keyword = match.Value.Trim();
            var normalized = keyword.Trim('.', ',', ';', ':', '(', ')', '[', ']', '{', '}');
            if (normalized.Length < 2 || StopWords.Contains(normalized))
            {
                continue;
            }

            scores[normalized] = scores.GetValueOrDefault(normalized) + 1;
            display.TryAdd(normalized, keyword);
        }

        return scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => display[pair.Key])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeKeywords(IEnumerable<string> keywords)
    {
        var uniqueKeywords = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords)
        {
            var normalized = NormalizeText(keyword).Trim(',', ';', ':');
            if (normalized.Length < 2 || !seen.Add(normalized))
            {
                continue;
            }

            uniqueKeywords.Add(normalized);
            if (uniqueKeywords.Count == 20)
            {
                break;
            }
        }

        return uniqueKeywords;
    }

    public sealed record JobDescriptionAnalysisResult(
        string CompanyDesc,
        IReadOnlyList<string> CompanyKeywords,
        bool UsedFallback,
        string? WarningMessage)
    {
        public static readonly JobDescriptionAnalysisResult Empty = new(string.Empty, Array.Empty<string>(), false, null);
    }

    private sealed class OllamaGenerateResponse
    {
        public string? Response { get; init; }

        public string? Thinking { get; init; }
    }
}