namespace JobAssistant.Core.Services;

public sealed class JobDescriptionEnrichmentException : Exception
{
    public JobDescriptionEnrichmentException(string message)
        : base(message)
    {
    }

    public JobDescriptionEnrichmentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}