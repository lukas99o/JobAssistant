namespace JobAssistant.Core.Models;

public sealed record Settings
{
    public double ActionDelay { get; init; } = 1.5;

    public int ApiBatchSize { get; init; } = 25;

    public string ApiBaseUrl { get; init; } = "https://jobsearch.api.jobtechdev.se";

    public bool BrowserHeadless { get; init; }

    public int BrowserSlowMo { get; init; } = 500;

    public bool AutoSubmit { get; init; }

    public int MaxSimpleFormFields { get; init; } = 10;

    public bool AutoAcceptCookies { get; init; } = true;
}