using AzBoardCodexTool.Commands;
using AzBoardCodexTool.Configuration;
using AzBoardCodexTool.Services;

try
{
    var options = AzureDevOpsOptions.FromEnvironment();

    using var httpClient = new HttpClient();
    var service = new AzureBoardsService(httpClient, options);
    var rootCommand = WorkItemCommands.CreateRootCommand(service);

    return await rootCommand.Parse(args).InvokeAsync();
}
catch (ConfigurationException ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 1;
}
