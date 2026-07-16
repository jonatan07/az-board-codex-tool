using AzBoardCodexTool.Services;

namespace AzBoardCodexTool.Tests;

public sealed class WorkItemTypeNameTests
{
    [Theory]
    [InlineData("Epic", "Epic")]
    [InlineData("userstory", "User Story")]
    [InlineData("Issue", "Issue")]
    [InlineData("PBI", "Product Backlog Item")]
    [InlineData("Requirement", "Requirement")]
    [InlineData("Task", "Task")]
    [InlineData("Bug", "Bug")]
    [InlineData("Impediment", "Impediment")]
    [InlineData("TestCase", "Test Case")]
    [InlineData("Custom Risk", "Custom Risk")]
    public void Normalize_ReturnsCanonicalOrCustomName(string input, string expected)
    {
        Assert.Equal(expected, WorkItemTypeName.Normalize(input));
    }

    [Fact]
    public void Normalize_RejectsEmptyType()
    {
        Assert.Throws<ArgumentException>(() => WorkItemTypeName.Normalize("   "));
    }
}
