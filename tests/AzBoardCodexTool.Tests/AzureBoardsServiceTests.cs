using System.Net;
using System.Text;
using AzBoardCodexTool.Configuration;
using AzBoardCodexTool.Services;

namespace AzBoardCodexTool.Tests;

public sealed class AzureBoardsServiceTests
{
    [Fact]
    public async Task CreateAsync_ResolvesPbiAliasAndCreatesCanonicalType()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Get && path.EndsWith("/workitemtypes", StringComparison.Ordinal))
            {
                return Json("""{"value":[{"name":"Product Backlog Item","referenceName":"Scrum.PBI"}]}""");
            }

            if (request.Method == HttpMethod.Get)
            {
                return Json("""{"value":[{"referenceName":"System.Title"}]}""");
            }

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/workitems/$Product%20Backlog%20Item", path, StringComparison.Ordinal);
            return Json("""{"id":42,"rev":1,"fields":{"System.WorkItemType":"Product Backlog Item"},"url":"https://example/42"}""");
        });
        var service = CreateService(handler);

        var item = await service.CreateAsync(
            "PBI", "Title", null, null, null, null, null, null, null, null, CancellationToken.None);

        Assert.Equal(42, item.Id);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsTypeUnavailableInProject()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Json("""{"value":[{"name":"Issue","referenceName":"System.Issue"}]}"""));
        var service = CreateService(handler);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Requirement", "Title", null, null, null, null, null, null, null, null, CancellationToken.None));

        Assert.Contains("Available types: Issue", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CreateAsync_AllowsCustomTypeExposedByProject()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Get && path.EndsWith("/workitemtypes", StringComparison.Ordinal))
            {
                return Json("""{"value":[{"name":"Custom Risk","referenceName":"Custom.Risk"}]}""");
            }

            if (request.Method == HttpMethod.Get)
            {
                return Json("""{"value":[{"referenceName":"System.Title"}]}""");
            }

            return Json("""{"id":7,"rev":1,"fields":{},"url":"https://example/7"}""");
        });
        var service = CreateService(handler);

        var item = await service.CreateAsync(
            "custom risk", "Risk", null, null, null, null, null, null, null, null, CancellationToken.None);

        Assert.Equal(7, item.Id);
    }

    [Fact]
    public async Task CreateAsync_RejectsOptionalFieldUnavailableForType()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            return path.EndsWith("/workitemtypes", StringComparison.Ordinal)
                ? Json("""{"value":[{"name":"Test Case","referenceName":"TestCase"}]}""")
                : Json("""{"value":[{"referenceName":"System.Title"}]}""");
        });
        var service = CreateService(handler);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Test Case", "Test", null, null, "Not supported", null, null, null, null, null, CancellationToken.None));

        Assert.Contains("AcceptanceCriteria", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.CallCount);
    }

    private static AzureBoardsService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new AzureDevOpsOptions("organization", "project", "secret"));

    private static HttpResponseMessage Json(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(handler(request));
        }
    }
}
