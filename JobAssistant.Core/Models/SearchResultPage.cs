namespace JobAssistant.Core.Models;

public sealed record SearchResultPage(IReadOnlyList<JobListing> Jobs, int Total);