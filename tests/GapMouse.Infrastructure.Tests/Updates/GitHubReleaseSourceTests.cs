using System.Net;
using System.Text;
using System.Text.Json;
using GapMouse.Infrastructure.Updates;

namespace GapMouse.Infrastructure.Tests.Updates;

public class GitHubReleaseSourceTests
{
    private const string Url = "https://api.github.com/repos/Gustavo-Piccoli/GapMouse/releases/latest";

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (GitHubReleaseSource Source, StubHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new GitHubReleaseSource(new HttpClient(handler), "1.0.0"), handler);
    }

    private static string Json(string? tag, string? url)
    {
        var parts = new List<string>();
        if (tag is not null)
        {
            parts.Add($"\"tag_name\":\"{tag}\"");
        }

        if (url is not null)
        {
            parts.Add($"\"html_url\":\"{url}\"");
        }

        return "{" + string.Join(',', parts) + "}";
    }

    [Fact]
    public async Task GetLatest_Ok_ParsesTagAndUrl()
    {
        var (source, _) = Create(HttpStatusCode.OK, Json("v1.2.3", "https://github.com/Gustavo-Piccoli/GapMouse/releases/tag/v1.2.3"));

        var info = await source.GetLatestAsync(TestContext.Current.CancellationToken);

        info.ShouldNotBeNull();
        info.TagName.ShouldBe("v1.2.3");
        info.PageUrl.ShouldBe(new Uri("https://github.com/Gustavo-Piccoli/GapMouse/releases/tag/v1.2.3"));
    }

    [Fact]
    public async Task GetLatest_SendsRequiredHeaders()
    {
        var (source, handler) = Create(HttpStatusCode.OK, Json("v1.0.0", "https://github.com/x/y"));

        await source.GetLatestAsync(TestContext.Current.CancellationToken);

        var request = handler.Request.ShouldNotBeNull();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri.ShouldBe(new Uri(Url));
        request.Headers.UserAgent.ToString().ShouldBe("GapMouse/1.0.0");
        request.Headers.GetValues("Accept").ShouldBe(["application/vnd.github+json"]);
        request.Headers.GetValues("X-GitHub-Api-Version").ShouldBe(["2022-11-28"]);
    }

    [Fact]
    public async Task GetLatest_NotFound_ReturnsNull()
    {
        var (source, _) = Create(HttpStatusCode.NotFound, "{}");

        (await source.GetLatestAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task GetLatest_RateLimited403_ThrowsHttpRequestException()
    {
        var (source, _) = Create(HttpStatusCode.Forbidden, "{}");

        await Should.ThrowAsync<HttpRequestException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatest_MalformedJson_ThrowsJsonException()
    {
        var (source, _) = Create(HttpStatusCode.OK, "{not json");

        await Should.ThrowAsync<JsonException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatest_MissingTag_ThrowsInvalidData()
    {
        var (source, _) = Create(HttpStatusCode.OK, Json(null, "https://github.com/x/y"));

        await Should.ThrowAsync<InvalidDataException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatest_NonGitHubUrl_ThrowsInvalidData()
    {
        var (source, _) = Create(HttpStatusCode.OK, Json("v1.0.0", "https://evil.example/x"));

        await Should.ThrowAsync<InvalidDataException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatest_HttpUrl_ThrowsInvalidData()
    {
        var (source, _) = Create(HttpStatusCode.OK, Json("v1.0.0", "http://github.com/x/y"));

        await Should.ThrowAsync<InvalidDataException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }
}
