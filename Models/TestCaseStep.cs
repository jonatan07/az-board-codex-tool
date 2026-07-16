using System.Text.Json.Serialization;

namespace AzBoardCodexTool.Models;

public sealed class TestCaseStep
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("expectedResult")]
    public string ExpectedResult { get; init; } = string.Empty;
}
