using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class JobHistoryStoreTests
{
    [Fact]
    public void Record_FilterNew_AndGetStats_PreserveCompatibilityShape()
    {
        var tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"jobassistant-history-{Guid.NewGuid():N}"));
        tempDirectory.Create();
        var historyFile = new FileInfo(Path.Combine(tempDirectory.FullName, "job_history.json"));

        try
        {
            var store = new JobHistoryStore(historyFile);

            var processedJob = new JobListing
            {
                Id = "123",
                Headline = "Backend Developer",
                EmployerName = "Acme AB",
                Description = "Build APIs",
            };

            var newJob = new JobListing
            {
                Id = "456",
                Headline = "Fullstack Developer",
                EmployerName = "Example AB",
                Description = "Build apps",
            };

            store.Record(processedJob, "applied", "c# developer", new DateOnly(2026, 5, 3));

            var json = File.ReadAllText(historyFile.FullName);
            Assert.Contains("\"job_id\"", json);
            Assert.Contains("\"company_name\"", json);
            Assert.True(store.IsProcessed("123"));

            var (newJobs, skippedCount) = store.FilterNew(new[] { processedJob, newJob });
            var stats = store.GetStats();

            Assert.Single(newJobs);
            Assert.Equal("456", newJobs[0].Id);
            Assert.Equal(1, skippedCount);
            Assert.Equal(1, stats["applied"]);
            Assert.Equal(1, stats["total"]);
        }
        finally
        {
            if (tempDirectory.Exists)
            {
                tempDirectory.Delete(recursive: true);
            }
        }
    }
}