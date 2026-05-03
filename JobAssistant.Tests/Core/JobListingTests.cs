using System.Text.Json;
using JobAssistant.Core.Models;

namespace JobAssistant.Tests.Core;

public sealed class JobListingTests
{
    [Fact]
    public void FromApiResponse_MapsExternalApplicationAndSummary()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "id": "123",
              "headline": "Fullstackutvecklare",
              "employer": { "name": "Acme AB" },
              "description": { "text": "Bygg moderna system." },
              "application_details": {
                "url": "https://example.com/apply",
                "email": null,
                "information": "Ansok via extern sida"
              },
              "workplace_address": { "city": "Stockholm" },
              "publication_date": "2026-05-03",
              "last_publication_date": "2026-05-10"
            }
            """);

        var listing = JobListing.FromApiResponse(document.RootElement);

        Assert.Equal("123", listing.Id);
        Assert.Equal("Fullstackutvecklare", listing.Headline);
        Assert.Equal("Acme AB", listing.EmployerName);
        Assert.Equal("external", listing.ApplicationMethod);
        Assert.Equal("Fullstackutvecklare at Acme AB", listing.Summary);
        Assert.Equal("Stockholm", listing.WorkplaceCity);
        Assert.Equal("https://example.com/apply", listing.ApplicationUrl);
    }

    [Fact]
    public void CompanyPurpose_TruncatesAtThreeThousandCharacters()
    {
        var listing = new JobListing
        {
            Description = new string('a', 3005),
        };

        Assert.Equal(3003, listing.CompanyPurpose.Length);
        Assert.EndsWith("...", listing.CompanyPurpose);
    }
}