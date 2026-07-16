using System.Text.Json.Serialization;

namespace AzBoardCodexTool.Models;

public sealed class WorkItemTypeListResponse
{
    [JsonPropertyName("value")]
    public List<WorkItemTypeDefinition> Value { get; init; } = [];
}

public sealed class WorkItemTypeDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("referenceName")]
    public string ReferenceName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class WorkItemTypeFieldListResponse
{
    [JsonPropertyName("value")]
    public List<WorkItemTypeFieldDefinition> Value { get; init; } = [];
}

public sealed class WorkItemTypeFieldDefinition
{
    [JsonPropertyName("referenceName")]
    public string ReferenceName { get; init; } = string.Empty;
}
