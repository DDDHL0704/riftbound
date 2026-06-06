using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void SerializeUsesCamelCasePropertyNames()
    {
        var json = CanonicalJson.Serialize(new CamelCaseProbe(
            "P1",
            7,
            new NestedProbe("MAIN_ACTION")));

        Assert.Equal(
            """{"playerId":"P1","serverTick":7,"nestedValue":{"promptType":"MAIN_ACTION"}}""",
            json);
    }

    [Fact]
    public void SerializeProducesCompactOutput()
    {
        var json = CanonicalJson.Serialize(new CompactProbe(
            3,
            [1, 2],
            new NestedProbe("READY")));

        Assert.Equal(
            """{"count":3,"values":[1,2],"nestedValue":{"promptType":"READY"}}""",
            json);
    }

    [Fact]
    public void SerializeKeepsNonAsciiAndHtmlSensitiveCharactersLiteral()
    {
        var json = CanonicalJson.Serialize(new EscapingProbe(
            "中文",
            "<tag>&"));

        Assert.Equal(
            """{"displayName":"中文","htmlSnippet":"<tag>&"}""",
            json);
    }

    private sealed record CamelCaseProbe(
        string PlayerId,
        int ServerTick,
        NestedProbe NestedValue);

    private sealed record CompactProbe(
        int Count,
        int[] Values,
        NestedProbe NestedValue);

    private sealed record EscapingProbe(
        string DisplayName,
        string HtmlSnippet);

    private sealed record NestedProbe(string PromptType);
}
