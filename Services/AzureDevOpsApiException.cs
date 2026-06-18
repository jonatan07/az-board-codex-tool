using System.Net;

namespace AzBoardCodexTool.Services;

public sealed class AzureDevOpsApiException(
    HttpStatusCode statusCode,
    string responseBody,
    string requestUri)
    : Exception($"Azure DevOps request failed with HTTP {(int)statusCode} ({statusCode}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
    public string RequestUri { get; } = requestUri;
}
