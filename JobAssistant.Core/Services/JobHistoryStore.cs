using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public sealed class JobHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    private readonly FileInfo _path;
    private Dictionary<string, JobHistoryRecord> _records = new(StringComparer.Ordinal);

    public JobHistoryStore(FileInfo path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        if (_path.Exists)
        {
            var json = File.ReadAllText(_path.FullName);
            var normalizedJson = NormalizeLegacySchema(json, out var migrated);
            _records = JsonSerializer.Deserialize<Dictionary<string, JobHistoryRecord>>(normalizedJson, SerializerOptions)
                ?? new Dictionary<string, JobHistoryRecord>(StringComparer.Ordinal);

            if (migrated)
            {
                Save();
            }

            return;
        }

        _records = new Dictionary<string, JobHistoryRecord>(StringComparer.Ordinal);
        Directory.CreateDirectory(_path.DirectoryName ?? throw new InvalidOperationException("History file path must have a parent directory."));
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(_path.DirectoryName ?? throw new InvalidOperationException("History file path must have a parent directory."));
        var json = JsonSerializer.Serialize(_records, SerializerOptions);
        File.WriteAllText(_path.FullName, json);
    }

    public bool IsProcessed(string jobId)
    {
        return _records.ContainsKey(jobId);
    }

    public void Record(JobListing job, string status, string searchQuery, DateOnly? searchDate = null)
    {
        var entry = new JobHistoryRecord
        {
            JobId = job.Id,
            CompanyName = job.EmployerName,
            Headline = job.Headline,
            CompanyDesc = job.CompanyDesc,
            CompanyKeywords = job.CompanyKeywords,
            Summary = job.Summary,
            LastSearchDate = (searchDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"),
            SearchQuery = searchQuery,
            Status = status,
        };

        _records[job.Id] = entry;
        Save();
    }

    public (List<JobListing> NewJobs, int SkippedCount) FilterNew(IEnumerable<JobListing> jobs)
    {
        var materializedJobs = jobs as IList<JobListing> ?? jobs.ToList();
        var newJobs = materializedJobs.Where(job => !IsProcessed(job.Id)).ToList();
        return (newJobs, materializedJobs.Count - newJobs.Count);
    }

    public Dictionary<string, int> GetStats()
    {
        var stats = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["applied"] = 0,
            ["skipped"] = 0,
            ["manual"] = 0,
            ["total"] = 0,
        };

        foreach (var record in _records.Values)
        {
            var status = string.IsNullOrWhiteSpace(record.Status) ? "skipped" : record.Status;
            stats[status] = stats.GetValueOrDefault(status) + 1;
            stats["total"] += 1;
        }

        return stats;
    }

    private static string NormalizeLegacySchema(string json, out bool migrated)
    {
        var rootNode = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (rootNode is not JsonObject rootObject)
        {
            migrated = false;
            return "{}";
        }

        migrated = false;

        foreach (var (_, node) in rootObject.ToList())
        {
            if (node is not JsonObject record)
            {
                continue;
            }

            if (!record.ContainsKey("company_desc") && record.TryGetPropertyValue("company_purpose", out var legacyCompanyPurpose))
            {
                record["company_desc"] = legacyCompanyPurpose?.DeepClone();
                migrated = true;
            }

            if (record.ContainsKey("company_purpose"))
            {
                record.Remove("company_purpose");
                migrated = true;
            }

            if (!record.TryGetPropertyValue("company_keywords", out var companyKeywordsNode) || companyKeywordsNode is null)
            {
                record["company_keywords"] = new JsonArray();
                migrated = true;
                continue;
            }

            if (companyKeywordsNode is JsonValue keywordValue)
            {
                var serializedKeywords = keywordValue.ToJsonString();
                var parsedKeywords = JsonSerializer.Deserialize<string>(serializedKeywords);
                if (!string.IsNullOrWhiteSpace(parsedKeywords))
                {
                    var array = new JsonArray();
                    foreach (var keyword in parsedKeywords.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        array.Add(keyword);
                    }

                    record["company_keywords"] = array;
                    migrated = true;
                }
            }
        }

        return rootObject.ToJsonString(SerializerOptions);
    }
}