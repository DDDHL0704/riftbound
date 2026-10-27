using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerAbilityTests
{
    [Theory]
    [InlineData("gate", false)] [InlineData("gate", true)]
    [InlineData("hook", false)] [InlineData("hook", true)]
    [InlineData("ward", false)] [InlineData("ward", true)]
    public async Task BankedEquipmentPowerPaysTheActualEquipmentAbilityIncludingItsTargetTax(string kind, bool spellResource)
    {
        var initial = Position(kind, spellResource); var game = new Driver(initial);
        ResolutionResult? core = null; bool? offered = null;
        try
        {
            await Bank(game, spellResource);
            var source = kind == "ward" ? "ORB" : "GEAR";
            var ability = kind switch {
                "gate" => P4ActivatedAbilityCatalog.NextSpellEchoAbilityId,
                "ward" => OrbOfRegretTests.Ability,
                _ => Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "OGN·242/298"
                    && a.Kind == ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest).AbilityId
            };
            string[] targets = kind switch { "ward" => ["D"], "hook" => ["HOST"], _ => [] };
            var command = new ActivateAbilityCommand(source, ability, targets);
            var prompt = ResolutionResult.BuildPrompts(game.State)["P1"];
            var requirement = (prompt.Candidates ?? []).Where(c => c.Action == CommandTypes.ActivateAbility)
                .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
                .SingleOrDefault(r => Equals(r["sourceObjectId"], source) && Equals(r["abilityId"], ability));
            offered = requirement is not null && (kind != "ward"
                || Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"])["0"].Any(c => c.Id == "D"));
            var before = MatchStateHasher.Hash(game.State); var count = game.Events().Length;
            core = await new CoreRuleEngine().ResolveAsync(game.State, new("restricted-equipment-core", "P1", command.CmdType), command, default);
            await Retained(game, command, !spellResource);
            Assert.Equal(!spellResource, core.Accepted); Assert.Equal(!spellResource, offered);
            if (!spellResource)
            {
                Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
                Assert.True(game.State.CardObjects[source].IsExhausted);
                var cost = Assert.Single(game.Events().Skip(count), e => e.Event.Kind == "COST_PAID").Event;
                Assert.Equal("EQUIPMENT", cost.Payload["powerPurpose"]);
                Assert.Equal(1, cost.Payload["totalPowerCost"]);
                if (kind == "gate") { await game.Top(); Assert.Contains(game.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1")); }
                if (kind == "ward") { Assert.Equal(1, cost.Payload["spellshieldTaxPower"]); await game.Top(); Assert.Equal(3, game.State.CardObjects["D"].Power); }
                // Hook payment is checked before resolving its separate private deck-play flow.
                if (kind == "hook") { Assert.Contains("HOST", game.State.PlayerZones["P1"].Base); Assert.Single(game.State.StackItems); }
            }
            else
            {
                Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(count, game.Events().Length);
                Assert.Equal(1, game.State.RunePools["P1"].SpellOnlyPower); Assert.False(game.State.CardObjects[source].IsExhausted);
            }
            await game.Replay();
        }
        finally { await Export(kind + "-" + spellResource, initial, game, core, offered); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NeitherRestrictedLegendBucketCanPayAnActualUnitAbility(bool spellResource)
    {
        var initial = ImposedMovementContinuationTests.WithPool(ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3)
            with { Tick = 0 }, new RunePool(2, 0));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1") } };
        initial = OfficialHoldSequenceTests.AddLegend(initial, spellResource ? "OGN·247/298" : "SFD·189/221");
        var game = new Driver(initial); await Bank(game, spellResource);
        var before = MatchStateHasher.Hash(game.State); var count = game.Events().Length;
        await Retained(game, new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, []), false);
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(count, game.Events().Length);
        Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static MatchState Position(string kind, bool spellResource)
    {
        // Explicit current turn-three local positions, not a complete legal opening.
        var state = kind == "ward" ? OrbOfRegretTests.Board(4, false)
            : kind == "hook" ? HookDeckPlayTests.Position() : ArmamentMightTests.Position("SFD·078/221");
        state = ImposedMovementContinuationTests.WithPool(state with { Tick = 0 }, new RunePool(kind == "hook" ? 1 : 0, 0));
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        if (kind == "ward") cards["D"] = OfficialGarenConquestBattlefieldCountTests.Printed("D", "SFD·037/221", "P2");
        else cards["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", kind == "hook" ? "OGN·242/298" : "SFD·078/221", "P1");
        state = state with { CardObjects = cards };
        if (kind == "ward" && cards.TryGetValue("LEGEND", out var opponentLegend) && opponentLegend.ControllerId == "P2")
        {
            const string opponentId = "P2-ORIGINAL-LEGEND";
            cards.Remove("LEGEND"); cards[opponentId] = opponentLegend with { ObjectId = opponentId };
            var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
            locations[opponentId] = locations.GetValueOrDefault("LEGEND", new ObjectLocationState("P2", "LEGEND"));
            locations.Remove("LEGEND");
            state = state with { CardObjects = cards, ObjectLocations = locations,
                PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
                    LegendZone = p.Value.LegendZone.Select(id => id == "LEGEND" ? opponentId : id).ToArray() }) };
        }
        return OfficialHoldSequenceTests.AddLegend(state, spellResource ? "OGN·247/298" : "SFD·189/221");
    }

    private static Task Bank(Driver game, bool spellResource) => game.Act("P1", new LegendActCommand("LEGEND",
        spellResource ? LegendActionAbilityCatalog.KaisaLegendAbilityId : LegendActionAbilityCatalog.OrnnLegendAbilityId, [], []));

    private static Task Retained(Driver game, GameCommand command, bool accepted)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new("P1", "restricted-ability-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), accepted);
    }

    private static async Task Export(string branch, MatchState initial, Driver game, ResolutionResult? core, bool? offered)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESTRICTED_LEGEND_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "equipment-ability-" + branch + ".json"), JsonSerializer.Serialize(new {
            initial, core, promptOffered = offered, driverState = game.State, commands = game.Commands(), events = game.Events(),
            scope = "Actual legend resource command and non-assemble equipment ability payment, current prompt/tick, original journal/recovery/dedup/replay; no whole-card or external acceptance."
        }, Json));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
