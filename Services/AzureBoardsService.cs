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

    public async Task<WorkItem> CreateAsync(
        string type,
        string title,
        string? description,
        string? assignedTo,
        string? acceptanceCriteria,
        string? stepsFile,
        int? priority,
        string? tags,
        string? comment,
        IReadOnlyCollection<string>? attachments,
        CancellationToken cancellationToken)
    {
        ValidateAttachments(attachments);

        var workItemType = await ResolveWorkItemTypeAsync(type, cancellationToken);
        var fields = await GetWorkItemTypeFieldsAsync(workItemType.Name, cancellationToken);
        var availableFields = fields
            .Select(field => field.ReferenceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var operations = new List<JsonPatchOperation>
        {
            new("add", "/fields/System.Title", title)
        };

        AddSupportedOptionalField(
            operations, availableFields, "System.Description", description, workItemType.Name);
        AddSupportedOptionalField(
            operations, availableFields, "System.AssignedTo", assignedTo, workItemType.Name);
        AddSupportedOptionalField(
            operations,
            availableFields,
            "Microsoft.VSTS.Common.AcceptanceCriteria",
            acceptanceCriteria,
            workItemType.Name);
        AddSupportedOptionalField(
            operations, availableFields, "System.Tags", tags, workItemType.Name);

        if (priority is not null)
        {
            EnsureFieldIsSupported(
                availableFields,
                "Microsoft.VSTS.Common.Priority",
                workItemType.Name);
            operations.Add(
                new JsonPatchOperation(
                    "add",
                    "/fields/Microsoft.VSTS.Common.Priority",
                    priority.Value));
        }

        if (!string.IsNullOrWhiteSpace(stepsFile))
        {
            if (!string.Equals(workItemType.Name, "Test Case", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("--steps-file can only be used with the Test Case type.");
            }

            EnsureFieldIsSupported(
                availableFields,
                "Microsoft.VSTS.TCM.Steps",
                workItemType.Name);
            operations.Add(
                new JsonPatchOperation(
                    "add",
                    "/fields/Microsoft.VSTS.TCM.Steps",
                    TestCaseStepsSerializer.FromJsonFile(stepsFile)));
        }

        var encodedType = Uri.EscapeDataString(workItemType.Name);
        return await CreateAndMaybeCommentAsync(
            HttpMethod.Post,
            $"_apis/wit/workitems/${encodedType}?api-version={ApiVersion}",
            operations,
            comment,
            attachments,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItemTypeDefinition>> GetWorkItemTypesAsync(
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"_apis/wit/workitemtypes?api-version={ApiVersion}",
            cancellationToken);
        var result = await ReadResponseAsync<WorkItemTypeListResponse>(
            response,
            cancellationToken);
        return result.Value
            .Where(type => !string.IsNullOrWhiteSpace(type.Name))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<WorkItem> UpdateAsync(
        int id,
        string? title,
        string? description,
        string? assignedTo,
        string? acceptanceCriteria,
        string? state,
        string? tags,
        string? comment,
        IReadOnlyCollection<string>? attachments,
        CancellationToken cancellationToken)
    {
        ValidateAttachments(attachments);

        var operations = new List<JsonPatchOperation>();
        AddOptionalField(operations, "/fields/System.Title", title);
        AddOptionalField(operations, "/fields/System.Description", description);
        AddOptionalField(operations, "/fields/System.AssignedTo", assignedTo);
        AddOptionalField(
            operations,
            "/fields/Microsoft.VSTS.Common.AcceptanceCriteria",
            acceptanceCriteria);
        AddOptionalField(operations, "/fields/System.State", state);
        AddOptionalField(operations, "/fields/System.Tags", tags);

        if (operations.Count == 0 &&
            string.IsNullOrWhiteSpace(comment) &&
            HasNoAttachments(attachments))
        {
            throw new ArgumentException(
                "At least one field, comment or attachment must be supplied: --title, --description, --assigned-to, --acceptance-criteria, --state, --tags, --comment or --attachment.");
        }

        return UpdateAndMaybeCommentAsync(id, operations, comment, attachments, cancellationToken);
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

    private async Task<WorkItem> CreateAndMaybeCommentAsync(
        HttpMethod method,
        string requestUri,
        IReadOnlyCollection<JsonPatchOperation> operations,
        string? comment,
        IReadOnlyCollection<string>? attachments,
        CancellationToken cancellationToken)
    {
        var item = await SendJsonPatchAsync(method, requestUri, operations, cancellationToken);
        if (!string.IsNullOrWhiteSpace(comment))
        {
            await AddCommentAsync(item.Id, comment, cancellationToken);
        }

        if (HasNoAttachments(attachments))
        {
            return item;
        }

        await AddAttachmentsAsync(item.Id, attachments!, cancellationToken);
        return await GetAsync(item.Id, cancellationToken);
    }

    private async Task<WorkItem> UpdateAndMaybeCommentAsync(
        int id,
        IReadOnlyCollection<JsonPatchOperation> operations,
        string? comment,
        IReadOnlyCollection<string>? attachments,
        CancellationToken cancellationToken)
    {
        WorkItem? item = null;
        if (operations.Count > 0)
        {
            item = await SendJsonPatchAsync(
                HttpMethod.Patch,
                $"_apis/wit/workitems/{id}?api-version={ApiVersion}",
                operations,
                cancellationToken);
        }
        else if (HasNoAttachments(attachments))
        {
            item = await GetAsync(id, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            await AddCommentAsync(id, comment, cancellationToken);
        }

        if (HasNoAttachments(attachments))
        {
            return item ?? await GetAsync(id, cancellationToken);
        }

        await AddAttachmentsAsync(id, attachments!, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task AddAttachmentsAsync(
        int workItemId,
        IReadOnlyCollection<string> attachments,
        CancellationToken cancellationToken)
    {
        foreach (var attachmentPath in attachments)
        {
            var attachmentUrl = await UploadAttachmentAsync(attachmentPath, cancellationToken);
            await LinkAttachmentAsync(workItemId, attachmentUrl, attachmentPath, cancellationToken);
        }
    }

    private async Task<string> UploadAttachmentAsync(
        string attachmentPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(attachmentPath))
        {
            throw new ArgumentException("Attachment path cannot be empty.", nameof(attachmentPath));
        }

        var fullPath = Path.GetFullPath(attachmentPath);

        var fileName = Path.GetFileName(fullPath);
        await using var fileStream = File.OpenRead(fullPath);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"_apis/wit/attachments?fileName={Uri.EscapeDataString(fileName)}&api-version={ApiVersion}")
        {
            Content = new StreamContent(fileStream)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var attachment = await ReadResponseAsync<AttachmentReference>(response, cancellationToken);
        return attachment.Url;
    }

    private Task<WorkItem> LinkAttachmentAsync(
        int workItemId,
        string attachmentUrl,
        string attachmentPath,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(attachmentPath);
        var operations = new List<JsonPatchOperation>
        {
            new(
                "add",
                "/relations/-",
                new
                {
                    rel = "AttachedFile",
                    url = attachmentUrl,
                    attributes = new { comment = $"Attached {fileName} by AzBoardCodexTool." }
                })
        };

        return SendJsonPatchAsync(
            HttpMethod.Patch,
            $"_apis/wit/workitems/{workItemId}?api-version={ApiVersion}",
            operations,
            cancellationToken);
    }

    private async Task AddCommentAsync(
        int workItemId,
        string comment,
        CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            $"_apis/wit/workItems/{workItemId}/comments?api-version=7.1-preview.4",
            JsonSerializer.Serialize(new { text = comment }, JsonOptions),
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ReadResponseBodyAsync(response, cancellationToken);
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

    private static async Task<string> ReadResponseBodyAsync(
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

        return body;
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

    private async Task<WorkItemTypeDefinition> ResolveWorkItemTypeAsync(
        string requestedType,
        CancellationToken cancellationToken)
    {
        var normalizedType = WorkItemTypeName.Normalize(requestedType);
        var types = await GetWorkItemTypesAsync(cancellationToken);
        var match = types.FirstOrDefault(
            type => string.Equals(type.Name, normalizedType, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return match;
        }

        var available = types.Count == 0
            ? "none"
            : string.Join(", ", types.Select(type => type.Name));
        throw new ArgumentException(
            $"Work item type '{requestedType}' is not available in project '{_options.Project}'. " +
            $"Available types: {available}.",
            nameof(requestedType));
    }

    private async Task<IReadOnlyList<WorkItemTypeFieldDefinition>> GetWorkItemTypeFieldsAsync(
        string type,
        CancellationToken cancellationToken)
    {
        var encodedType = Uri.EscapeDataString(type);
        using var response = await _httpClient.GetAsync(
            $"_apis/wit/workitemtypes/{encodedType}/fields?api-version={ApiVersion}",
            cancellationToken);
        var result = await ReadResponseAsync<WorkItemTypeFieldListResponse>(
            response,
            cancellationToken);
        return result.Value;
    }

    private static void AddSupportedOptionalField(
        ICollection<JsonPatchOperation> operations,
        IReadOnlySet<string> availableFields,
        string referenceName,
        string? value,
        string workItemType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        EnsureFieldIsSupported(availableFields, referenceName, workItemType);
        operations.Add(new JsonPatchOperation("add", $"/fields/{referenceName}", value));
    }

    private static void EnsureFieldIsSupported(
        IReadOnlySet<string> availableFields,
        string referenceName,
        string workItemType)
    {
        if (!availableFields.Contains(referenceName))
        {
            throw new ArgumentException(
                $"Field '{referenceName}' is not available for work item type '{workItemType}'.");
        }
    }

    private static bool HasNoAttachments(IReadOnlyCollection<string>? attachments) =>
        attachments is null || attachments.Count == 0;

    private static void ValidateAttachments(IReadOnlyCollection<string>? attachments)
    {
        if (HasNoAttachments(attachments))
        {
            return;
        }

        foreach (var attachmentPath in attachments!)
        {
            if (string.IsNullOrWhiteSpace(attachmentPath))
            {
                throw new ArgumentException("Attachment path cannot be empty.", nameof(attachments));
            }

            var fullPath = Path.GetFullPath(attachmentPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Attachment file was not found.", fullPath);
            }
        }
    }

}

public sealed class AttachmentReference
{
    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
