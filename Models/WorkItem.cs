using System.Text.Json.Serialization;

namespace AzBoardCodexTool.Models;

public sealed class WorkItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("rev")]
    public int Revision { get; init; }

    [JsonPropertyName("fields")]
    public Dictionary<string, object?> Fields { get; init; } = [];

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("_links")]
    public Dictionary<string, LinkReference>? Links { get; init; }

    [JsonPropertyName("relations")]
    public List<WorkItemRelation>? Relations { get; init; }
}

public sealed class LinkReference
{
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}

public sealed class WorkItemRelation
{
    [JsonPropertyName("rel")]
    public string Relationship { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, object?>? Attributes { get; init; }
}
