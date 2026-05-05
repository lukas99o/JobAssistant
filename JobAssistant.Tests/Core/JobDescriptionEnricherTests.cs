using System.Net;
using System.Net.Http;
using System.Text;
using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class JobDescriptionEnricherTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesOllamaResponse_WhenValidJsonIsReturned()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                                            "response": "{\"company_desc\":\"Kort sammanfattning av rollen.\\n\\nFokus på moderna molntjänster.\",\"company_keywords\":[\"C#\",\".NET\",\"Azure\"]}"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });

        using var client = new HttpClient(handler);
        using var enricher = new JobDescriptionEnricher(new Settings(), client);

        var result = await enricher.AnalyzeAsync("Vi söker en .NET-utvecklare med Azure-erfarenhet.");

        Assert.False(result.UsedFallback);
        Assert.Equal("Kort sammanfattning av rollen." + Environment.NewLine + Environment.NewLine + "Fokus på moderna molntjänster.", result.CompanyDesc);
        Assert.Equal(new[] { "C#", ".NET", "Azure" }, result.CompanyKeywords);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesThinkingPayload_WhenResponseIsEmpty()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "response": "",
                      "thinking": "{\"company_desc\":\"Kort sammanfattning via thinking-faltet.\",\"company_keywords\":[\".NET\",\"Azure\"]}"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });

        using var client = new HttpClient(handler);
        using var enricher = new JobDescriptionEnricher(new Settings(), client);

        var result = await enricher.AnalyzeAsync("Vi söker en .NET-utvecklare med Azure-erfarenhet.");

        Assert.False(result.UsedFallback);
        Assert.Equal("Kort sammanfattning via thinking-faltet.", result.CompanyDesc);
        Assert.Equal(new[] { ".NET", "Azure" }, result.CompanyKeywords);
    }

    [Fact]
    public async Task AnalyzeAsync_Throws_WhenOllamaIsUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));

        using var client = new HttpClient(handler);
        using var enricher = new JobDescriptionEnricher(new Settings(), client);

        var exception = await Assert.ThrowsAsync<JobDescriptionEnrichmentException>(() => enricher.AnalyzeAsync("We build APIs with C# .NET Azure and Kubernetes."));

        Assert.Contains("Ollama enrichment failed", exception.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesExplicitFallback_WhenOllamaIsDisabled()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not be called")));
        using var enricher = new JobDescriptionEnricher(new Settings { OllamaEnabled = false }, client);

        var result = await enricher.AnalyzeAsync("We build APIs with C# .NET Azure and Kubernetes.");

        Assert.True(result.UsedFallback);
        Assert.Equal("Ollama enrichment is disabled in settings. Using local extraction.", result.WarningMessage);
        Assert.NotEmpty(result.CompanyDesc);
        Assert.NotEmpty(result.CompanyKeywords);
    }

    [Fact]
    public async Task EnsureReadyAsync_Throws_WhenOllamaReturnsUnreadableResponse()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"response\":\"not valid structured json\"}", Encoding.UTF8, "application/json"),
            });

        using var client = new HttpClient(handler);
        using var enricher = new JobDescriptionEnricher(new Settings(), client);

        var exception = await Assert.ThrowsAsync<JobDescriptionEnrichmentException>(() => enricher.EnsureReadyAsync());

        Assert.Contains("unreadable response", exception.Message);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}