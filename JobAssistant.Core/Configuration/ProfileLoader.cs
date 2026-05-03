using JobAssistant.Core.Models;
using YamlDotNet.Serialization;

namespace JobAssistant.Core.Configuration;

public sealed class ProfileLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public UserProfile Load(FileInfo path)
    {
        if (!path.Exists)
        {
            return new UserProfile();
        }

        using var reader = path.OpenText();
        var document = _deserializer.Deserialize<ProfileYamlDocument>(reader) ?? new ProfileYamlDocument();

        return new UserProfile
        {
            FirstName = document.Personal?.FirstName ?? string.Empty,
            LastName = document.Personal?.LastName ?? string.Empty,
            Email = document.Personal?.Email ?? string.Empty,
            Phone = document.Personal?.Phone ?? string.Empty,
            Street = document.Personal?.Address?.Street ?? string.Empty,
            City = document.Personal?.Address?.City ?? string.Empty,
            PostalCode = document.Personal?.Address?.PostalCode ?? string.Empty,
            Country = document.Personal?.Address?.Country ?? string.Empty,
            Organization = document.Personal?.Organization ?? string.Empty,
            Title = document.Professional?.Title ?? string.Empty,
            ProfessionalSummary = document.Professional?.Summary ?? string.Empty,
            Linkedin = document.Links?.Linkedin ?? string.Empty,
            Github = document.Links?.Github ?? string.Empty,
            Website = document.Links?.Website ?? string.Empty,
            FormAnswers = new ProfileFormAnswers
            {
                Languages = ToDictionary(document.FormAnswers?.Languages),
                YesNo = ToDictionary(document.FormAnswers?.YesNo),
                Text = ToDictionary(document.FormAnswers?.Text),
            },
        };
    }

    private static Dictionary<string, string> ToDictionary(Dictionary<string, object?>? source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (source is null)
        {
            return result;
        }

        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            result[key] = value.ToString() ?? string.Empty;
        }

        return result;
    }

    private sealed class ProfileYamlDocument
    {
        [YamlMember(Alias = "personal")]
        public PersonalYamlDocument? Personal { get; init; }

        [YamlMember(Alias = "professional")]
        public ProfessionalYamlDocument? Professional { get; init; }

        [YamlMember(Alias = "links")]
        public LinksYamlDocument? Links { get; init; }

        [YamlMember(Alias = "form_answers")]
        public FormAnswersYamlDocument? FormAnswers { get; init; }
    }

    private sealed class PersonalYamlDocument
    {
        [YamlMember(Alias = "first_name")]
        public string? FirstName { get; init; }

        [YamlMember(Alias = "last_name")]
        public string? LastName { get; init; }

        [YamlMember(Alias = "email")]
        public string? Email { get; init; }

        [YamlMember(Alias = "phone")]
        public string? Phone { get; init; }

        [YamlMember(Alias = "organization")]
        public string? Organization { get; init; }

        [YamlMember(Alias = "address")]
        public AddressYamlDocument? Address { get; init; }
    }

    private sealed class AddressYamlDocument
    {
        [YamlMember(Alias = "street")]
        public string? Street { get; init; }

        [YamlMember(Alias = "city")]
        public string? City { get; init; }

        [YamlMember(Alias = "postal_code")]
        public string? PostalCode { get; init; }

        [YamlMember(Alias = "country")]
        public string? Country { get; init; }
    }

    private sealed class ProfessionalYamlDocument
    {
        [YamlMember(Alias = "title")]
        public string? Title { get; init; }

        [YamlMember(Alias = "summary")]
        public string? Summary { get; init; }
    }

    private sealed class LinksYamlDocument
    {
        [YamlMember(Alias = "linkedin")]
        public string? Linkedin { get; init; }

        [YamlMember(Alias = "github")]
        public string? Github { get; init; }

        [YamlMember(Alias = "website")]
        public string? Website { get; init; }
    }

    private sealed class FormAnswersYamlDocument
    {
        [YamlMember(Alias = "languages")]
        public Dictionary<string, object?>? Languages { get; init; }

        [YamlMember(Alias = "yes_no")]
        public Dictionary<string, object?>? YesNo { get; init; }

        [YamlMember(Alias = "text")]
        public Dictionary<string, object?>? Text { get; init; }
    }
}