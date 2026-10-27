using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedResourceConversionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task EquipmentPowerCanPayTheActualEquipmentConversionButSpellPowerCannot(bool spellResource, bool during)
    {
        var initial = Position(spellResource, P4ActivatedAbilityCatalog.HextechAnomalyCardNo);
        if (!during) initial = WithPool(initial, RunePool.Empty);
        var game = new Driver(initial); ResolutionResult? core = null; bool? offered = null;
        try
        {
            await game.Act("P1", new LegendActCommand("LEGEND", spellResource
                ? LegendActionAbilityCatalog.KaisaLegendAbilityId : LegendActionAbilityCatalog.OrnnLegendAbilityId, [], []));
            if (during) { await game.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"])); await game.Top(); }
            var choice = P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "1";
            var resourceChoice = $"RESOURCE:GEAR:{P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId}:{choice}";
            GameCommand command = during ? new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [resourceChoice])
                : new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, [], [choice]);
            offered = during ? game.State.PendingRuleChoice!.Request.Options.Any(o => o.Id == resourceChoice)
                : JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P1"], Web).Contains('"' + choice + '"', StringComparison.Ordinal);
            var before = MatchStateHasher.Hash(game.State);
            core = await new CoreRuleEngine().ResolveAsync(game.State, new("restricted-conversion-core", "P1", command.CmdType), command, default);
            await Retained(game, command, !spellResource);
            Assert.Equal(!spellResource, core.Accepted); Assert.Equal(!spellResource, offered);
            if (!spellResource)
            {
                Assert.Equal(new RunePool(1, 0), game.State.RunePools["P1"]);
                Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
                Assert.Single(game.Events(), e => e.Event.Kind == "MANA_GAINED"
                    && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "GEAR"));
            }
            else
            {
                Assert.Equal(before, MatchStateHasher.Hash(game.State));
                Assert.Equal(1, game.State.RunePools["P1"].SpellOnlyPower);
                Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
            }
            if (during) await Decide(game, "SPEND_POWER:0");
            Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
            await game.Replay();
        }
        finally { await Export("conversion-" + spellResource + "-" + during, initial, game, core, offered); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AFullRestrictedPoolDoesNotOfferAnOverflowingEquipmentResource(bool initiallyFull)
    {
        var initial = Position(true, P4ActivatedAbilityCatalog.HoneyfruitCardNo);
        initial = WithPool(initial, new RunePool(1, 0, spellOnlyPower: int.MaxValue - (initiallyFull ? 0 : 1)));
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"])); await game.Top();
            var gain = $"RESOURCE:GEAR:{P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId}:";
            var legend = $"LEGEND_RESOURCE:LEGEND:{LegendActionAbilityCatalog.KaisaLegendAbilityId}";
            if (!initiallyFull)
            {
                Assert.Contains(game.State.PendingRuleChoice!.Request.Options, o => o.Id == gain);
                Assert.Contains(game.State.PendingRuleChoice.Request.Options, o => o.Id == legend);
                await Decide(game, legend);
            }
            Assert.Equal(int.MaxValue, game.State.RunePools["P1"].TotalStoredPower);
            Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == gain || o.Id == legend);
            await Retained(game, new PayCostCommand(game.State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", [gain]), false);
            Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
            await Decide(game, "SPEND_POWER:0");
            Assert.Equal(int.MaxValue, game.State.RunePools["P1"].TotalStoredPower);
            Assert.True(game.State.RunePools["P1"].HasSupportedAmounts);
            await game.Replay();
        }
        finally { await Export("capacity-" + initiallyFull, initial, game, null, null); }
    }

    private static MatchState Position(bool spellResource, string gear)
    {
        var state = OfficialHoldSequenceTests.AddLegend(InstructionPowerPaymentTimingTests.Position(),
            spellResource ? "OGN·247/298" : "SFD·189/221");
        // Explicit entered, ready equipment in turn three; Honeyfruit's entry
        // dormancy has already ended. This is a numeric boundary, not an opening.
        return state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "GEAR"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", gear, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P1", "BASE") }
        };
    }

    private static MatchState WithPool(MatchState state, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };

    private static Task Decide(Driver game, string choice)
        => game.Act("P1", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [choice]));

    private static Task Retained(Driver game, GameCommand command, bool accepted)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new("P1", "restricted-conversion-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Web)), accepted);
    }

    private static async Task Export(string branch, MatchState initial, Driver game, ResolutionResult? core, bool? offered)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESTRICTED_LEGEND_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, branch + ".json"), JsonSerializer.Serialize(new {
            scope = "Current CN Ornn/Kaisa and SFD083/UNL049 source slice; entered equipment, real resource commands, exact payment purpose and combined capacity. Original journal, recovery and dedup; no complete opening, L2, L3 or whole-card certification.",
            initial, state = game.State, core, offered, commands = game.Commands(), events = game.Events()
        }, new JsonSerializerOptions(Web) { WriteIndented = true }));
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
