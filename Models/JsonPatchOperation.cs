using System.Text.Json.Serialization;

namespace AzBoardCodexTool.Models;

public sealed record JsonPatchOperation(
    [property: JsonPropertyName("op")] string Operation,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("value")] object Value);
