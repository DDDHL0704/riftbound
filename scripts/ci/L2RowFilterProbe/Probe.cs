using System.Text.Json;
using Xunit;

namespace WorkflowToolProbe;

public sealed class CaseFilterProbe
{
    public static IEnumerable<object[]> Colors => new[] { "red", "green", "blue", "orange", "purple", "yellow" }
        .Select(value => new object[] { value });
    public static IEnumerable<object[]> Edges => new[] { "a|b&(c)=!~\\xyz", "a,b \"q\" 中文" }
        .Select(value => new object[] { value });
    public static IEnumerable<object[]> OpaqueRows => new[] { new OpaqueRow(1), new OpaqueRow(2) }
        .Select(value => new object[] { value });

    [Theory, MemberData(nameof(Colors))]
    public void Expanded(string key) => Audit("Expanded:" + key);
    [Theory, MemberData(nameof(Edges))]
    public void Escaped(string key) => Audit("Escaped:" + key);
    [Theory, InlineData("case"), InlineData("CASE")]
    public void CaseFoldCollision(string key) => Audit("Collision:" + key);
    [Theory, MemberData(nameof(OpaqueRows))]
    public void Unexpanded(OpaqueRow row) => Audit("Opaque:" + row.Id);
    [Fact]
    public void WholeMethod() => Audit("Fact:single");

    private static void Audit(string key)
    {
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name!.StartsWith("Riftbound.", StringComparison.Ordinal));
        var allowed = JsonSerializer.Deserialize<string[]>(Environment.GetEnvironmentVariable("ROW_PROBE_ALLOWED")!)!;
        Assert.Contains(key, allowed);
        File.AppendAllText(Environment.GetEnvironmentVariable("ROW_PROBE_JOURNAL")!, JsonSerializer.Serialize(key) + "\n");
    }
}

// Intentionally not serializable: runtime-only MemberData must stay a whole method.
public sealed class OpaqueRow(int id)
{
    public int Id { get; } = id;
    public override string ToString() => "Opaque(" + Id + ")";
}
