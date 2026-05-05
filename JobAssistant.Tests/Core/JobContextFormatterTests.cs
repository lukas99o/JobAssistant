using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class JobContextFormatterTests
{
    [Fact]
    public void FormatForContextWindow_IncludesStructuredSections()
    {
        var job = new JobListing
        {
            Headline = "Fullstackutvecklare",
            EmployerName = "Acme AB",
            Description = "Ange referens teamtailor-7677525-1980472 i ditt personliga brev.",
            WorkplaceCity = "Stockholm",
            CompanyDesc = "Bygg moderna system med fokus på .NET och Azure.",
            CompanyKeywords = [".NET", "Azure", "React"],
            ApplicationUrl = "https://example.com/apply",
            ApplicationInfo = "Ange referens i ansokan.",
        };

        var result = JobContextFormatter.FormatForContextWindow(job);

        Assert.Contains("Job Context", result);
        Assert.Contains("Role: Fullstackutvecklare", result);
        Assert.Contains("Company: Acme AB", result);
        Assert.Contains("Location: Stockholm", result);
        Assert.Contains("Application URL: https://example.com/apply", result);
        Assert.Contains("Application Notes:", result);
        Assert.Contains("Include this reference in your personal letter:", result);
        Assert.Contains("teamtailor-7677525-1980472", result);
        Assert.Contains("Ange referens i ansokan.", result);
        Assert.Contains("Summary:", result);
        Assert.Contains("Bygg moderna system med fokus på .NET", result);
        Assert.Contains("och Azure.", result);
        Assert.Contains("Keywords:", result);
        Assert.Contains("  - .NET", result);
        Assert.Contains("  - Azure", result);
        Assert.Contains("  - React", result);
    }

    [Fact]
    public void FormatForContextWindow_ReturnsEmpty_ForNullJob()
    {
        var result = JobContextFormatter.FormatForContextWindow(job: null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetWindowTitle_UsesRoleAndCompany()
    {
        var job = new JobListing
        {
            Headline = "Backendutvecklare",
            EmployerName = "Northwind AB",
        };

        var result = JobContextFormatter.GetWindowTitle(job);

        Assert.Equal("Backendutvecklare | Northwind AB", result);
    }
}