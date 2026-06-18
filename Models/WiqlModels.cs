using System.Text.Json.Serialization;

namespace AzBoardCodexTool.Models;

public sealed class WiqlResponse
{
    [JsonPropertyName("workItems")]
    public List<WorkItemReference> WorkItems { get; init; } = [];
}

public sealed class WorkItemReference
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}

public sealed class WorkItemBatchResponse
{
    [JsonPropertyName("value")]
    public List<WorkItem> Value { get; init; } = [];
}
