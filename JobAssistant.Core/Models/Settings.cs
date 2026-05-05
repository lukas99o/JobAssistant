namespace JobAssistant.Core.Models;

public sealed record Settings
{
    public double ActionDelay { get; init; } = 1.5;

    public bool PersonalLetterEditorEnabled { get; init; } = true;

    public int ApiBatchSize { get; init; } = 25;

    public string ApiBaseUrl { get; init; } = "https://jobsearch.api.jobtechdev.se";

    public bool OllamaEnabled { get; init; } = true;

    public string OllamaBaseUrl { get; init; } = "http://127.0.0.1:11434";

    public string OllamaModel { get; init; } = "qwen2.5:9b";

    public int OllamaTimeoutSeconds { get; init; } = 90;

    public bool BrowserHeadless { get; init; }

    public int BrowserSlowMo { get; init; } = 500;

    public bool AutoSubmit { get; init; }

    public int MaxSimpleFormFields { get; init; } = 10;

    public bool AutoAcceptCookies { get; init; } = true;
}