using System.Text.Json;
using System.Xml.Linq;
using AzBoardCodexTool.Models;

namespace AzBoardCodexTool.Services;

public static class TestCaseStepsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string FromJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Test case steps file cannot be empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Test case steps file was not found.", fullPath);
        }

        var steps = JsonSerializer.Deserialize<List<TestCaseStep>>(
            File.ReadAllText(fullPath),
            JsonOptions) ?? throw new ArgumentException("Test case steps JSON cannot be null.", nameof(path));

        if (steps.Count == 0)
        {
            throw new ArgumentException("Test case steps JSON must contain at least one step.", nameof(path));
        }

        var root = new XElement(
            "steps",
            new XAttribute("id", "0"),
            new XAttribute("last", steps.Count));

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                throw new ArgumentException(
                    $"Test case step {index + 1} must include a non-empty action.",
                    nameof(path));
            }

            root.Add(
                new XElement(
                    "step",
                    new XAttribute("id", index + 1),
                    new XAttribute("type", "ActionStep"),
                    FormattedValue(step.Action),
                    FormattedValue(step.ExpectedResult),
                    new XElement("description")));
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement FormattedValue(string value) =>
        new(
            "parameterizedString",
            new XAttribute("isformatted", "true"),
            new XElement("DIV", value ?? string.Empty).ToString(SaveOptions.DisableFormatting));
}
