using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzBoardCodexTool.Configuration;
using AzBoardCodexTool.Models;

namespace AzBoardCodexTool.Services;

public sealed class AzureBoardsService
{
    private const string ApiVersion = "7.1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;

    public AzureBoardsService(HttpClient httpClient, AzureDevOpsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _httpClient.BaseAddress = options.ProjectBaseUri;
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var basicToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicToken);
    }

    public Task<WorkItem> CreateAsync(
        string type,
        string title,
        string? description,
        string? tags,
        CancellationToken cancellationToken)
    {
        var operations = new List<JsonPatchOperation>
        {
            new("add", "/fields/System.Title", title)
        };

        AddOptionalField(operations, "/fields/System.Description", description);
        AddOptionalField(operations, "/fields/System.Tags", tags);

        var encodedType = Uri.EscapeDataString(NormalizeWorkItemType(type));
        return SendJsonPatchAsync(
            HttpMethod.Post,
            $"_apis/wit/workitems/${encodedType}?api-version={ApiVersion}",
            operations,
            cancellationToken);
    }

    public Task<WorkItem> UpdateAsync(
        int id,
        string? title,
        string? description,
        string? state,
        string? tags,
        CancellationToken cancellationToken)
    {
        var operations = new List<JsonPatchOperation>();
        AddOptionalField(operations, "/fields/System.Title", title);
        AddOptionalField(operations, "/fields/System.Description", description);
        AddOptionalField(operations, "/fields/System.State", state);
        AddOptionalField(operations, "/fields/System.Tags", tags);

        if (operations.Count == 0)
        {
            throw new ArgumentException(
                "At least one field must be supplied: --title, --description, --state or --tags.");
        }

        return SendJsonPatchAsync(
            HttpMethod.Patch,
            $"_apis/wit/workitems/{id}?api-version={ApiVersion}",
            operations,
            cancellationToken);
    }

    public async Task<WorkItem> GetAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"_apis/wit/workitems/{id}?$expand=relations&api-version={ApiVersion}",
            cancellationToken);

        return await ReadResponseAsync<WorkItem>(response, cancellationToken);
    }

    public Task<WorkItem> LinkParentAsync(
        int childId,
        int parentId,
        CancellationToken cancellationToken)
    {
        var parentUrl = new Uri(
            _options.OrganizationBaseUri,
            $"_apis/wit/workItems/{parentId}").ToString();

        var operations = new List<JsonPatchOperation>
        {
            new(
                "add",
                "/relations/-",
                new
                {
                    rel = "System.LinkTypes.Hierarchy-Reverse",
                    url = parentUrl,
                    attributes = new { comment = $"Linked by AzBoardCodexTool to parent {parentId}." }
                })
        };

        return SendJsonPatchAsync(
            HttpMethod.Patch,
            $"_apis/wit/workitems/{childId}?api-version={ApiVersion}",
            operations,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> QueryAsync(
        string wiql,
        CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            $"_apis/wit/wiql?api-version={ApiVersion}",
            JsonSerializer.Serialize(new { query = wiql }, JsonOptions),
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var queryResult = await ReadResponseAsync<WiqlResponse>(response, cancellationToken);

        if (queryResult.WorkItems.Count == 0)
        {
            return [];
        }

        var result = new List<WorkItem>(queryResult.WorkItems.Count);
        foreach (var batch in queryResult.WorkItems.Select(item => item.Id).Chunk(200))
        {
            using var batchRequest = CreateJsonRequest(
                HttpMethod.Post,
                $"_apis/wit/workitemsbatch?api-version={ApiVersion}",
                JsonSerializer.Serialize(
                    new
                    {
                        ids = batch,
                        fields = new[]
                        {
                            "System.Id",
                            "System.WorkItemType",
                            "System.Title",
                            "System.State",
                            "System.AssignedTo",
                            "System.Tags",
                            "System.ChangedDate"
                        }
                    },
                    JsonOptions),
                "application/json");

            using var batchResponse = await _httpClient.SendAsync(batchRequest, cancellationToken);
            var batchResult =
                await ReadResponseAsync<WorkItemBatchResponse>(batchResponse, cancellationToken);
            result.AddRange(batchResult.Value);
        }

        return result;
    }

    private async Task<WorkItem> SendJsonPatchAsync(
        HttpMethod method,
        string requestUri,
        IReadOnlyCollection<JsonPatchOperation> operations,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(operations, JsonOptions);
        using var request = CreateJsonRequest(
            method,
            requestUri,
            json,
            "application/json-patch+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadResponseAsync<WorkItem>(response, cancellationToken);
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string requestUri,
        string json,
        string mediaType)
    {
        return new HttpRequestMessage(method, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AzureDevOpsApiException(
                response.StatusCode,
                body,
                response.RequestMessage?.RequestUri?.ToString() ?? "unknown");
        }

        var value = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return value ?? throw new InvalidOperationException(
            "Azure DevOps returned an empty or invalid JSON response.");
    }

    private static void AddOptionalField(
        ICollection<JsonPatchOperation> operations,
        string path,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            operations.Add(new JsonPatchOperation("add", path, value));
        }
    }

    private static string NormalizeWorkItemType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "epic" => "Epic",
            "feature" => "Feature",
            "user story" or "userstory" or "story" => "User Story",
            "task" => "Task",
            "bug" => "Bug",
            _ => throw new ArgumentException(
                "Invalid work item type. Supported values: Epic, Feature, User Story, Task, Bug.")
        };
    }
}
