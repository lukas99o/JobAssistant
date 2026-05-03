namespace JobAssistant.Core.Models;

public sealed record ProfileFormAnswers
{
    public Dictionary<string, string> Languages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> YesNo { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Text { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record UserProfile
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string Organization { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ProfessionalSummary { get; init; } = string.Empty;

    public string Linkedin { get; init; } = string.Empty;

    public string Github { get; init; } = string.Empty;

    public string Website { get; init; } = string.Empty;

    public ProfileFormAnswers FormAnswers { get; init; } = new();

    public string FullName => $"{FirstName} {LastName}".Trim();

    public List<string> Validate()
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(FirstName))
        {
            warnings.Add("First name is empty");
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            warnings.Add("Last name is empty");
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            warnings.Add("Email is empty");
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            warnings.Add("Phone is empty");
        }

        return warnings;
    }
}