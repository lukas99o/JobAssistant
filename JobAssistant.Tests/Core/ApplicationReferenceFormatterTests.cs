using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class ApplicationReferenceFormatterTests
{
    [Fact]
    public void ExtractReference_FindsStructuredReferenceInApplicationInfo()
    {
        var job = new JobListing
        {
            ApplicationInfo = "Ange referens: teamtailor-7677525-1980472 i ditt personliga brev.",
        };

        var result = ApplicationReferenceFormatter.ExtractReference(job);

        Assert.Equal("teamtailor-7677525-1980472", result);
    }

    [Fact]
    public void BuildApplicationNotes_IncludesReferenceAndRelevantDescriptionLine()
    {
        var job = new JobListing
        {
            ApplicationInfo = "Ansok via extern sida.",
            Description = "Bygg moderna system.\nAnge referens teamtailor-7677525-1980472 i ansokan.\nPlacering Stockholm.",
        };

        var result = ApplicationReferenceFormatter.BuildApplicationNotes(job);

        Assert.Contains("Include this reference in your personal letter: teamtailor-7677525-1980472", result);
        Assert.Contains("Ansok via extern sida.", result);
        Assert.Contains("Ange referens teamtailor-7677525-1980472 i ansokan.", result);
    }

    [Fact]
    public void PrependReferenceToPersonalLetter_AddsReferenceHeader_WhenMissing()
    {
        var job = new JobListing
        {
            Description = "Ange referens teamtailor-7677525-1980472 i ansokan.",
        };

        var result = ApplicationReferenceFormatter.PrependReferenceToPersonalLetter("Hej,\n\nJag soker rollen.", job);

        Assert.StartsWith("Referens: teamtailor-7677525-1980472", result, StringComparison.Ordinal);
        Assert.Contains("Jag soker rollen.", result);
    }

    [Fact]
    public void ExtractRelevantInstruction_ReturnsReferenceLineFromPageText()
    {
        var pageText = "Ansok har.\nKom ihag att ange referens teamtailor-7677525-1980472 i ditt personliga brev.\nLycka till.";

        var result = ApplicationReferenceFormatter.ExtractRelevantInstruction(pageText);

        Assert.Equal("Kom ihag att ange referens teamtailor-7677525-1980472 i ditt personliga brev.", result);
    }
}