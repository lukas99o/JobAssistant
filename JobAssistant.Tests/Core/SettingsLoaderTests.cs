using JobAssistant.Core.Configuration;

namespace JobAssistant.Tests.Core;

public sealed class SettingsLoaderTests
{
    [Fact]
    public void Load_MapsOllamaSettings()
    {
        var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"jobassistant-settings-{Guid.NewGuid():N}.yaml"));

        File.WriteAllText(
            tempFile.FullName,
            """
            ollama_enabled: false
            ollama_base_url: http://localhost:11434
            ollama_model: qwen2.5:9b
            ollama_timeout_seconds: 42
            auto_submit: true
            personal_letter_editor_enabled: false
            """);

        try
        {
            var loader = new SettingsLoader();
            var settings = loader.Load(tempFile);

            Assert.False(settings.OllamaEnabled);
            Assert.Equal("http://localhost:11434", settings.OllamaBaseUrl);
            Assert.Equal("qwen2.5:9b", settings.OllamaModel);
            Assert.Equal(42, settings.OllamaTimeoutSeconds);
            Assert.True(settings.AutoSubmit);
            Assert.False(settings.PersonalLetterEditorEnabled);
        }
        finally
        {
            if (tempFile.Exists)
            {
                tempFile.Delete();
            }
        }
    }

    [Fact]
    public void Load_UsesDefaultPersonalLetterEditorSetting_WhenMissing()
    {
        var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"jobassistant-settings-{Guid.NewGuid():N}.yaml"));

        File.WriteAllText(tempFile.FullName, "auto_submit: false");

        try
        {
            var loader = new SettingsLoader();
            var settings = loader.Load(tempFile);

            Assert.True(settings.PersonalLetterEditorEnabled);
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