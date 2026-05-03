using JobAssistant.Core.Configuration;

namespace JobAssistant.Tests.Core;

public sealed class ProfileLoaderTests
{
    [Fact]
    public void Load_MapsProfileYamlIntoStronglyTypedModel()
    {
        var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"jobassistant-profile-{Guid.NewGuid():N}.yaml"));

        File.WriteAllText(
            tempFile.FullName,
            """
            personal:
              first_name: Ada
              last_name: Lovelace
              email: ada@example.com
              phone: "123456"
              organization: Example Org
              address:
                street: Main Street 1
                city: Stockholm
                postal_code: "11122"
                country: Sweden

            professional:
              title: Developer
              summary: Builds systems.

            links:
              linkedin: https://linkedin.example/ada
              github: https://github.example/ada
              website: https://ada.example

            form_answers:
              languages:
                swedish: Fluent
              yes_no:
                work_permit: Yes
              text:
                salary: Negotiable
            """);

        try
        {
            var loader = new ProfileLoader();
            var profile = loader.Load(tempFile);

            Assert.Equal("Ada", profile.FirstName);
            Assert.Equal("Lovelace", profile.LastName);
            Assert.Equal("Ada Lovelace", profile.FullName);
            Assert.Equal("Stockholm", profile.City);
            Assert.Equal("Developer", profile.Title);
            Assert.Equal("Fluent", profile.FormAnswers.Languages["swedish"]);
            Assert.Equal("Yes", profile.FormAnswers.YesNo["work_permit"]);
            Assert.Equal("Negotiable", profile.FormAnswers.Text["salary"]);
            Assert.Empty(profile.Validate());
        }
        finally
        {
            if (tempFile.Exists)
            {
                tempFile.Delete();
            }
        }
    }
}