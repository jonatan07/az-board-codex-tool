using System.Text.Json;
using AzBoardCodexTool.Models;
using AzBoardCodexTool.Services;

namespace AzBoardCodexTool.Commands;

internal static class ConsoleOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (AzureDevOpsApiException ex)
        {
            Console.Error.WriteLine(
                $"HTTP {(int)ex.StatusCode} ({ex.StatusCode}) calling {ex.RequestUri}");
            Console.Error.WriteLine(ex.ResponseBody);
            Environment.ExitCode = 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Validation error: {ex.Message}");
            Environment.ExitCode = 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Validation error: {ex.Message}");
            Environment.ExitCode = 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Validation error: Invalid JSON. {ex.Message}");
            Environment.ExitCode = 2;
        }
    }

    public static void PrintCreated(WorkItem item)
    {
        Console.WriteLine($"Created Work Item ID: {item.Id}");
        Console.WriteLine($"URL: {GetWebUrl(item)}");
    }

    public static void PrintUpdated(WorkItem item, string operation)
    {
        Console.WriteLine($"{operation} Work Item ID: {item.Id}");
        Console.WriteLine($"URL: {GetWebUrl(item)}");
    }

    public static void PrintJson(object value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    public static void PrintQueryResults(IReadOnlyList<WorkItem> items)
    {
        if (items.Count == 0)
        {
            Console.WriteLine("No work items found.");
            return;
        }

        Console.WriteLine("ID\tType\tState\tTitle");
        foreach (var item in items)
        {
            Console.WriteLine(
                $"{item.Id}\t{Field(item, "System.WorkItemType")}\t" +
                $"{Field(item, "System.State")}\t{Field(item, "System.Title")}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {items.Count}");
    }

    public static void PrintWorkItemTypes(IReadOnlyList<WorkItemTypeDefinition> types)
    {
        if (types.Count == 0)
        {
            Console.WriteLine("No work item types are available in the configured project.");
            return;
        }

        Console.WriteLine("Available Work Item Types:");
        foreach (var type in types)
        {
            Console.WriteLine($"- {type.Name}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {types.Count}");
    }

    private static string GetWebUrl(WorkItem item)
    {
        if (item.Links is not null &&
            item.Links.TryGetValue("html", out var html) &&
            !string.IsNullOrWhiteSpace(html.Href))
        {
            return html.Href;
        }

        return item.Url;
    }

    private static string Field(WorkItem item, string name) =>
        item.Fields.TryGetValue(name, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
}
