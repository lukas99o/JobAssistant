using JobAssistant.Core.Models;
using YamlDotNet.Serialization;
using JobSettings = JobAssistant.Core.Models.Settings;

namespace JobAssistant.Core.Configuration;

public sealed class SettingsLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public JobSettings Load(FileInfo path)
    {
        if (!path.Exists)
        {
            return new JobSettings();
        }

        using var reader = path.OpenText();
        var document = _deserializer.Deserialize<SettingsYamlDocument>(reader) ?? new SettingsYamlDocument();

        return new JobSettings
        {
            ActionDelay = document.ActionDelay ?? 1.5,
            PersonalLetterEditorEnabled = document.PersonalLetterEditorEnabled ?? true,
            ApiBatchSize = document.ApiBatchSize ?? 25,
            ApiBaseUrl = document.ApiBaseUrl ?? "https://jobsearch.api.jobtechdev.se",
            OllamaEnabled = document.OllamaEnabled ?? true,
            OllamaBaseUrl = document.OllamaBaseUrl ?? "http://127.0.0.1:11434",
            OllamaModel = document.OllamaModel ?? "qwen2.5:9b",
            OllamaTimeoutSeconds = document.OllamaTimeoutSeconds ?? 90,
            BrowserHeadless = document.BrowserHeadless ?? false,
            BrowserSlowMo = document.BrowserSlowMo ?? 500,
            AutoSubmit = document.AutoSubmit ?? false,
            MaxSimpleFormFields = document.MaxSimpleFormFields ?? 10,
            AutoAcceptCookies = document.AutoAcceptCookies ?? true,
        };
    }

    private sealed class SettingsYamlDocument
    {
        [YamlMember(Alias = "action_delay")]
        public double? ActionDelay { get; init; }

        [YamlMember(Alias = "personal_letter_editor_enabled")]
        public bool? PersonalLetterEditorEnabled { get; init; }

        [YamlMember(Alias = "api_batch_size")]
        public int? ApiBatchSize { get; init; }

        [YamlMember(Alias = "api_base_url")]
        public string? ApiBaseUrl { get; init; }

        [YamlMember(Alias = "ollama_enabled")]
        public bool? OllamaEnabled { get; init; }

        [YamlMember(Alias = "ollama_base_url")]
        public string? OllamaBaseUrl { get; init; }

        [YamlMember(Alias = "ollama_model")]
        public string? OllamaModel { get; init; }

        [YamlMember(Alias = "ollama_timeout_seconds")]
        public int? OllamaTimeoutSeconds { get; init; }

        [YamlMember(Alias = "browser_headless")]
        public bool? BrowserHeadless { get; init; }

        [YamlMember(Alias = "browser_slow_mo")]
        public int? BrowserSlowMo { get; init; }

        [YamlMember(Alias = "auto_submit")]
        public bool? AutoSubmit { get; init; }

        [YamlMember(Alias = "max_simple_form_fields")]
        public int? MaxSimpleFormFields { get; init; }

        [YamlMember(Alias = "auto_accept_cookies")]
        public bool? AutoAcceptCookies { get; init; }
    }
}