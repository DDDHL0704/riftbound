using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Current CN356.4.c applies the passive component discount as the selected
// additional fee is added. Haste contains both resource dimensions, so the
// player must choose its mana/power allocation before a price is authorized.
public sealed class MandatoryOptionalCostReductionTests
{
    [Theory]
    [InlineData("SFD·149/221", "none", 0, 0)]
    [InlineData("SFD·149a/221", "none", 0, 0)]
    [InlineData("SFD·149/221", "MANA", 2, 1)]
    [InlineData("SFD·149a/221", "MANA", 2, 1)]
    [InlineData("SFD·149/221", "POWER-red", 3, 0)]
    [InlineData("SFD·149a/221", "POWER-red", 3, 0)]
    public async Task DeclaredHasteRequiresItsMandatorySourceReductionAllocation(
        string reducer, string allocation, int expectedMana, int expectedRedPower)
    {
        var initial = Position(reducer);
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var session = new MatchSession(initial, engine, journal);
        var prompt = session.PromptFor("P1");
        var costs = allocation == "none" ? new[] { HasteOptionalCostNames.HasteReady }
            : new[] { HasteOptionalCostNames.HasteReady, "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:" + allocation };
        var command = new PlayCardCommand("CARD", "OGN·010/298", [], OptionalCosts: costs);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var raw = JsonSerializer.SerializeToNode(command, json)!.AsObject();
        raw["promptId"] = prompt.PromptId;
        raw["snapshotTick"] = initial.Tick;
        var wire = JsonSerializer.SerializeToElement(raw, json);
        var before = MatchStateHasher.Hash(initial);
        var quote = engine.PreviewPlayCard(initial, "P1", new("mandatory-reduction-quote", prompt.PromptId!, initial.Tick, command));
        var core = await engine.ResolveAsync(initial, new("mandatory-reduction-core", "P1", command.CmdType), command, default);
        var native = await session.SubmitAsync("P1", "mandatory-reduction-native", GameCommandJsonMapper.Map(wire), wire, default);
        try
        {
            var payable = allocation != "none";
            Assert.Equal(payable, quote.IsValid);
            Assert.Equal(payable, quote.CanPay);
            Assert.Equal(payable, core.Accepted);
            Assert.Equal(payable, native.Accepted);
            Assert.Equal(before, MatchStateHasher.Hash(initial));
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(native);
            Assert.Equal(payable, Assert.Single(journal.Entries).Accepted);
            if (!payable)
            {
                Assert.Empty(core.Events);
                Assert.Empty(native.Events);
                Assert.Equal(before, MatchStateHasher.Hash(core.State));
                Assert.Equal(before, MatchStateHasher.Hash(native.State));
                return;
            }
            Assert.Equal(expectedMana, quote.Cost!.Mana);
            Assert.Equal(expectedRedPower, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red));
            foreach (var result in new[] { core, native })
            {
                var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
                Assert.Equal(expectedMana, paid.Payload["mana"]);
                Assert.Equal(expectedRedPower, paid.Payload["power"]);
                Assert.Equal(3 - expectedMana, result.State.RunePools["P1"].Mana);
                Assert.Equal(1 - expectedRedPower, result.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
                Assert.Contains(costs[1], Assert.IsAssignableFrom<IEnumerable<string>>(paid.Payload["costReductionChoices"]));
                Assert.DoesNotContain("CARD", result.State.PlayerZones["P1"].Hand);
                Assert.Contains("CARD", result.State.PlayerZones["P1"].Base);
            }
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_MANDATORY_DISCOUNT_EVIDENCE") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory,
                    reducer.Replace('·', '-').Replace('/', '-') + "-" + allocation + ".json"),
                    JsonSerializer.Serialize(new { scope = "Declared normal midgame Core/native fee component; not canonical room birth.",
                        initial, prompt, command, quote, core, native, wire, journal = journal.Entries }, json));
            }
        }
    }

    private static MatchState Position(string reducer)
        => new("MANDATORY-OPTIONAL-REDUCTION", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Base = ["EZREAL"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["CARD"] = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", "OGN·010/298", "P1"),
                ["EZREAL"] = OfficialGarenConquestBattlefieldCountTests.Printed("EZREAL", reducer, "P1") },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["EZREAL"] = new("P1", "BASE") });

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
