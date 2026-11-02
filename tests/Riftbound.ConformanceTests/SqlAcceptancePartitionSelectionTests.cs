using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SqlAcceptancePartitionSelectionTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "sql-partition-selection-" + Guid.NewGuid().ToString("N"));
    private readonly string manifest;
    private static readonly string[] Ids = ["core-opening", "first", "second", "third", "fourth"];

    public SqlAcceptancePartitionSelectionTests()
    {
        Directory.CreateDirectory(folder);
        manifest = Path.Combine(folder, "manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new {
            schemaVersion = 2, kind = "SQL_EXECUTION_IDENTITIES_NOT_RULE_CERTIFICATION", defaultPartitionCount = 2,
            cases = Ids.Select((id, ordinal) => new { id,
                sourceType = ordinal == 0 ? "PROGRAM_CORE_OPENING" : "SyntheticSelectionUnitFixture",
                defaultPartition = new[] { 1, 1, 0, 0, 1 }[ordinal] }) }));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public async Task DefaultAssignmentsSelectEveryCaseOnceAndPreserveItsOriginalExecutionOrder()
    {
        var all = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var run = SqlAcceptancePartition.Parse(["--ci-partition", index.ToString(), "2"], manifest);
            var actual = new List<string>();
            foreach (var id in Ids) await run.RunAsync(id, () => { actual.Add(id); return Task.CompletedTask; });
            Assert.True(run.IsPartition);
            Assert.Equal(index == 0 ? new[] { "second", "third" } : new[] { "core-opening", "first", "fourth" }, actual);
            Assert.Equal(run.Expected(), actual);
            all.AddRange(actual);
        }
        Assert.Equal(Ids.Order(), all.Order());
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void CustomPartitionCountsRetainModuloSelection(int count)
    {
        for (var index = 0; index < count; index++)
        {
            var run = SqlAcceptancePartition.Parse(["--ci-partition", index.ToString(), count.ToString()], manifest);
            Assert.Equal(Ids.Where((_, ordinal) => ordinal % count == index), run.Expected());
        }
    }

    [Fact]
    public async Task UnpartitionedExecutionRetainsEveryOriginalCallInOrder()
    {
        var run = SqlAcceptancePartition.Parse([], manifest);
        Assert.False(run.IsPartition);
        Assert.Equal(Ids, run.Expected());
        var actual = new List<string>();
        foreach (var id in Ids) await run.RunAsync(id, () => { actual.Add(id); return Task.CompletedTask; });
        Assert.Equal(Ids, actual);
    }

    [Fact]
    public async Task UnknownOrRepeatedCallsFailEvenWhenTheirPartitionIsUnselected()
    {
        var run = SqlAcceptancePartition.Parse(["--ci-partition", "0", "2"], manifest);
        var calls = 0;
        Task Count() { calls++; return Task.CompletedTask; }
        await run.RunAsync("first", Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.RunAsync("first", Count));
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.RunAsync("not-in-the-manifest", Count));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ReorderedSelectedCallsCannotProduceACompletionReceipt()
    {
        var run = SqlAcceptancePartition.Parse(["--ci-partition", "0", "2"], manifest);
        foreach (var id in new[] { "core-opening", "first", "third", "second", "fourth" })
            await run.RunAsync(id, () => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.CompleteAsync(folder));
        Assert.False(File.Exists(Path.Combine(folder, "partition-receipt.json")));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingOrFailedSelectedCallsCannotProduceACompletionReceipt(bool fail)
    {
        var run = SqlAcceptancePartition.Parse(["--ci-partition", "0", "2"], manifest);
        foreach (var id in Ids)
        {
            if (id == "second")
            {
                if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    run.RunAsync(id, () => Task.FromException(new InvalidOperationException("Synthetic unit failure"))));
            }
            else await run.RunAsync(id, () => Task.CompletedTask);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.CompleteAsync(folder));
        Assert.False(File.Exists(Path.Combine(folder, "partition-receipt.json")));
    }

    [Theory]
    [InlineData("missing")] [InlineData("negative")] [InlineData("outside")] [InlineData("empty-partition")]
    [InlineData("duplicate")] [InlineData("legacy-schema")] [InlineData("missing-core")] [InlineData("too-many-partitions")]
    public void IncompleteOrInvalidAssignmentPlansFailClosed(string mutation)
    {
        var data = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        var cases = data["cases"]!.AsArray();
        switch (mutation)
        {
            case "missing": cases[0]!.AsObject().Remove("defaultPartition"); break;
            case "negative": cases[0]!["defaultPartition"] = -1; break;
            case "outside": cases[0]!["defaultPartition"] = 2; break;
            case "empty-partition": foreach (var row in cases) row!["defaultPartition"] = 1; break;
            case "duplicate": cases[1]!["id"] = "core-opening"; break;
            case "legacy-schema": data["schemaVersion"] = 1; break;
            case "missing-core": cases[0]!["id"] = "no-core"; break;
            case "too-many-partitions": data["defaultPartitionCount"] = 6; break;
            default: throw new ArgumentException(mutation);
        }
        File.WriteAllText(manifest, data.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => SqlAcceptancePartition.Parse([], manifest));
    }

    [Fact]
    public void InvalidPartitionArgumentsFailClosed()
    {
        foreach (var args in new[] { new[] { "--ci-partition" }, new[] { "--ci-partition", "-1", "2" },
                     new[] { "--ci-partition", "0", "0" }, new[] { "--ci-partition", "5", "5" }, new[] { "--ci-partition", "0", "6" } })
            Assert.Throws<ArgumentException>(() => SqlAcceptancePartition.Parse(args, manifest));
    }

    [Fact]
    public void CommittedPlanKeepsAllSixtyNineIdentitiesAcrossFourNonemptyPartitions()
    {
        using var plan = JsonDocument.Parse(SqlAcceptancePartition.PlanJson());
        var rows = plan.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var ids = rows.Select(row => row.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(69, ids.Length);
        Assert.Equal(69, ids.Distinct().Count());
        Assert.Equal(4, plan.RootElement.GetProperty("defaultPartitionCount").GetInt32());
        var all = new List<string>();
        for (var index = 0; index < 4; index++)
        {
            var selected = SqlAcceptancePartition.Parse(["--ci-partition", index.ToString(), "4"]).Expected();
            Assert.Equal(rows.Where(row => row.GetProperty("defaultPartition").GetInt32() == index)
                .Select(row => row.GetProperty("id").GetString()!), selected);
            Assert.InRange(selected.Length, 17, 18);
            all.AddRange(selected);
        }
        Assert.Equal(ids.Order(), all.Order());
        Assert.Equal(ids, SqlAcceptancePartition.Parse([]).Expected());
    }
}
