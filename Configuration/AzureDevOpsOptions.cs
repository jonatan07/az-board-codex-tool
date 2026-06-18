namespace AzBoardCodexTool.Configuration;

public sealed record AzureDevOpsOptions(string Organization, string Project, string PersonalAccessToken)
{
    public Uri ProjectBaseUri =>
        new($"https://dev.azure.com/{Uri.EscapeDataString(Organization)}/{Uri.EscapeDataString(Project)}/");

    public Uri OrganizationBaseUri =>
        new($"https://dev.azure.com/{Uri.EscapeDataString(Organization)}/");

    public static AzureDevOpsOptions FromEnvironment()
    {
        var missing = new List<string>();

        var organization = ReadRequired("AZDO_ORG", missing);
        var project = ReadRequired("AZDO_PROJECT", missing);
        var pat = ReadRequired("AZDO_PAT", missing);

        if (missing.Count > 0)
        {
            throw new ConfigurationException(
                $"Missing required environment variable(s): {string.Join(", ", missing)}.");
        }

        return new AzureDevOpsOptions(organization!, project!, pat!);
    }

    private static string? ReadRequired(string name, ICollection<string> missing)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
            return null;
        }

        return value.Trim();
    }
}
