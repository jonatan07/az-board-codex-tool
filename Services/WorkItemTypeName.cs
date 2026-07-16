namespace AzBoardCodexTool.Services;

public static class WorkItemTypeName
{
    public static string Normalize(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Work item type cannot be empty.", nameof(type));
        }

        var trimmed = type.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "epic" => "Epic",
            "feature" => "Feature",
            "user story" or "userstory" or "story" => "User Story",
            "issue" => "Issue",
            "product backlog item" or "productbacklogitem" or "pbi" =>
                "Product Backlog Item",
            "requirement" => "Requirement",
            "task" => "Task",
            "bug" => "Bug",
            "impediment" => "Impediment",
            "test case" or "testcase" => "Test Case",
            _ => trimmed
        };
    }
}
