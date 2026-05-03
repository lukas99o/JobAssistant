using JobAssistant.Core.Models;
using Microsoft.Playwright;
using PlaywrightProgram = Microsoft.Playwright.Program;

namespace JobAssistant.Browser;

public sealed class BrowserManager : IAsyncDisposable
{
    private static readonly string[] ApplySelectors =
    {
        "a:has-text('Apply here')",
        "a:has-text('Ansök här')",
        "a:has-text('Apply now')",
        "a:has-text('Ansök nu')",
        "a:has-text('Apply')",
        "a:has-text('Ansök')",
        "button:has-text('Apply here')",
        "button:has-text('Ansök här')",
        "button:has-text('Apply now')",
        "button:has-text('Ansök nu')",
        "button:has-text('Apply')",
        "button:has-text('Ansök')",
        "button:has-text('Sök nu')",
        "button:has-text('Sök')",
    };

    private static readonly string[] CookieSelectors =
    {
        "button:has-text('Accept')",
        "button:has-text('Acceptera')",
        "button:has-text('Godkänn')",
        "button:has-text('Accept all')",
        "button:has-text('Acceptera alla')",
        "button:has-text('Allow all')",
        "button:has-text('I agree')",
        "button:has-text('OK')",
        "button:has-text('Got it')",
        "button:has-text('Agree')",
        "[id*='cookie'] button",
        "[class*='cookie'] button",
        "[id*='consent'] button",
        "[class*='consent'] button",
    };

    private readonly Settings _settings;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public BrowserManager(Settings settings)
    {
        _settings = settings;
    }

    public IPage Page => _page ?? throw new InvalidOperationException("Browser not started. Call StartAsync() first.");

    public async Task StartAsync()
    {
        if (_page is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchBrowserAsync();
        _page = await _browser.NewPageAsync();
        Console.WriteLine("  Browser started.");
    }

    public async Task<IPage> NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        var page = Page;

        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000,
        });

        if (_settings.AutoAcceptCookies)
        {
            await DismissCookiesAsync();
        }

        await DelayForActionAsync(cancellationToken);
        return page;
    }

    public async Task<bool> TryClickApplyButtonAsync(CancellationToken cancellationToken = default)
    {
        var page = Page;

        foreach (var selector in ApplySelectors)
        {
            try
            {
                var button = page.Locator(selector).First;
                if (!await IsVisibleWithinAsync(button, 1000))
                {
                    continue;
                }

                await button.ClickAsync();
                Console.WriteLine("  Apply button clicked, loading next page...");
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                {
                    Timeout = 15000,
                });

                if (_settings.AutoAcceptCookies)
                {
                    await DismissCookiesAsync();
                }

                await DelayForActionAsync(cancellationToken);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    public async Task CloseAsync()
    {
        if (_page is not null)
        {
            try
            {
                await _page.CloseAsync();
            }
            catch
            {
            }

            _page = null;
        }

        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync();
            }
            catch
            {
            }

            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        Console.WriteLine("  Browser closed.");
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    private async Task DismissCookiesAsync()
    {
        var page = Page;

        foreach (var selector in CookieSelectors)
        {
            try
            {
                var button = page.Locator(selector).First;
                if (!await IsVisibleWithinAsync(button, 1000))
                {
                    continue;
                }

                await button.ClickAsync();
                Console.WriteLine("  Cookies accepted.");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                return;
            }
            catch
            {
            }
        }
    }

    private async Task DelayForActionAsync(CancellationToken cancellationToken)
    {
        if (_settings.ActionDelay <= 0)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(_settings.ActionDelay), cancellationToken);
    }

    private static async Task<bool> IsVisibleWithinAsync(ILocator locator, float timeout)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeout,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IBrowser> LaunchBrowserAsync()
    {
        try
        {
            return await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _settings.BrowserHeadless,
                SlowMo = _settings.BrowserSlowMo,
            });
        }
        catch (PlaywrightException exception) when (IsMissingBrowserExecutable(exception))
        {
            Console.WriteLine("  Playwright browser not installed. Installing Chromium...");
            await InstallChromiumAsync();

            return await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _settings.BrowserHeadless,
                SlowMo = _settings.BrowserSlowMo,
            });
        }
    }

    private static bool IsMissingBrowserExecutable(PlaywrightException exception)
    {
        return exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("ms-playwright", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InstallChromiumAsync()
    {
        var exitCode = await Task.Run(() => PlaywrightProgram.Main(new[] { "install", "chromium" }));
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }
    }
}