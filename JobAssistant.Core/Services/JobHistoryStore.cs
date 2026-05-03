using System.Text.Encodings.Web;
using System.Text.Json;
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
            _records = JsonSerializer.Deserialize<Dictionary<string, JobHistoryRecord>>(json, SerializerOptions)
                ?? new Dictionary<string, JobHistoryRecord>(StringComparer.Ordinal);
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
            CompanyPurpose = job.CompanyPurpose,
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
}