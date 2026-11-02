using System.Net;
using System.Text;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class OllamaReleaseCheckerTests
{
    [Fact]
    public async Task CheckAsync_ComparaVersionInstaladaConReleasePublicado()
    {
        using var http = new HttpClient(new StubHandler("{\"tag_name\":\"v0.40.13\"}"));
        using var checker = new OllamaReleaseChecker(http);

        var result = await checker.CheckAsync("0.40.12");

        Assert.True(result.CheckSucceeded);
        Assert.True(result.UpdateAvailable);
        Assert.Equal("0.40.13", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_VersionActualizada_NoRecomiendaActualizar()
    {
        using var http = new HttpClient(new StubHandler("{\"tag_name\":\"v0.40.13\"}"));
        using var checker = new OllamaReleaseChecker(http);

        var result = await checker.CheckAsync("0.40.13");

        Assert.True(result.CheckSucceeded);
        Assert.False(result.UpdateAvailable);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/ollama/ollama/releases/latest", request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
