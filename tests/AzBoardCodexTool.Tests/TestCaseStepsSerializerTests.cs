using System.Xml.Linq;
using AzBoardCodexTool.Services;

namespace AzBoardCodexTool.Tests;

public sealed class TestCaseStepsSerializerTests
{
    [Fact]
    public void FromJsonFile_CreatesAzureTestStepsXml()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                """
                [
                  { "action": "Open <home>", "expectedResult": "Home is visible" },
                  { "action": "Sign in", "expectedResult": "Dashboard appears" }
                ]
                """);

            var xml = TestCaseStepsSerializer.FromJsonFile(path);
            var document = XDocument.Parse(xml);

            Assert.Equal("2", document.Root?.Attribute("last")?.Value);
            Assert.Equal(2, document.Descendants("step").Count());
            Assert.Contains(
                "<DIV>Open &lt;home&gt;</DIV>",
                document.Descendants("parameterizedString").First().Value,
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
