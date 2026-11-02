using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class OllamaInventoryParsingTests
{
    [Fact]
    public void ParseCapabilities_LeeCapacidadesRealesDeApiShow()
    {
        var capabilities = OllamaTagsParser.ParseCapabilities("""
            {"capabilities":["completion","tools","thinking","vision"]}
            """);

        Assert.Equal(new[] { "completion", "tools", "thinking", "vision" }, capabilities);
    }

    [Fact]
    public void ParseCliList_LeeElFormatoDeOllama04013SinFlagsJson()
    {
        const string output = """
            NAME                    ID              SIZE      MODIFIED
            lfm3:1.7b-q4_K_M         9fcf...         1.1 GB    3 days ago
            granite4.2:3b            271a...         2.2 GB    2 days ago
            """;

        var models = OllamaTagsParser.ParseCliList(output);

        Assert.Equal(2, models.Count);
        Assert.Equal("lfm3:1.7b-q4_K_M", models[0].Name);
        Assert.InRange(models[0].SizeBytes, 1_180_000_000, 1_190_000_000);
        Assert.Equal("granite4.2:3b", models[1].Name);
    }
}
