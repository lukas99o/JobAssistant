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
                Description = "Build APIs. Ange referens teamtailor-7677525-1980472 i din ansokan.",
                CompanyDesc = "Build APIs and integrations.",
                CompanyKeywords = new[] { "APIs", "Integrations" },
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
            Assert.Contains("\"company_desc\"", json);
            Assert.Contains("\"company_keywords\"", json);
            Assert.Contains("\"application_reference\"", json);
            Assert.Contains("teamtailor-7677525-1980472", json);
            Assert.DoesNotContain("\"company_purpose\"", json);
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

    [Fact]
    public void Load_MigratesLegacyCompanyPurposeEntries()
    {
        var tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"jobassistant-history-{Guid.NewGuid():N}"));
        tempDirectory.Create();
        var historyFile = new FileInfo(Path.Combine(tempDirectory.FullName, "job_history.json"));

        File.WriteAllText(
            historyFile.FullName,
            """
            {
              "legacy-job": {
                "job_id": "legacy-job",
                "company_name": "Legacy AB",
                "headline": "Senior Developer",
                "company_purpose": "Legacy description",
                "summary": "Senior Developer at Legacy AB",
                "last_search_date": "2026-05-04",
                "search_query": "dotnet stockholm",
                "status": "manual"
              }
            }
            """);

        try
        {
            var store = new JobHistoryStore(historyFile);

            Assert.True(store.IsProcessed("legacy-job"));

            var migratedJson = File.ReadAllText(historyFile.FullName);
            Assert.Contains("\"company_desc\"", migratedJson);
            Assert.Contains("\"company_keywords\": []", migratedJson);
            Assert.Contains("\"application_reference\": \"\"", migratedJson);
            Assert.DoesNotContain("\"company_purpose\"", migratedJson);
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