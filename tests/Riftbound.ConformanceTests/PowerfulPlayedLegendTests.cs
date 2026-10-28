using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulPlayedLegendTests
{
    private static MatchState Position(string legend)
    {
        var state = OfficialCopyIdentityTests.AddSpell(ArmamentMightTests.Position(), "PLAYED", "SFD·175/221");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PLAYED"] = state.CardObjects["PLAYED"] with { Tags = [CardObjectTags.UnitCard] },
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        return PrintedCostFixture.Add(state, "P1", "SFD·175/221");
    }

    private static async Task<ResolutionResult> Open(string legend, MatchState? initial = null, bool legendFirst = true)
    {
        var state = initial ?? Position(legend);
        var played = await Act(state, "P1", new PlayCardCommand("PLAYED", "SFD·175/221", []));
        void Capture(string step, ResolutionResult result)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_POWERFUL_PLAYED_DIAGNOSTICS") is not { } folder) return;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, legend.Replace('/', '-') + "-" + step + ".json"),
                JsonSerializer.Serialize(new { result.Accepted, result.ErrorCode, result.ErrorMessage, result.State, result.Events },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        Capture("play", played);
        var own = Assert.Single(played.State.TriggerQueue, t => t.SourceObjectId == "PLAYED");
        var legendTrigger = Assert.Single(played.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var ordered = await Act(played.State, "P1", new OrderTriggersCommand(legendFirst
            ? [legendTrigger.TriggerId, own.TriggerId] : [own.TriggerId, legendTrigger.TriggerId]));
        Capture("ordered", ordered);
        return ordered;
    }

    [Theory]
    [InlineData("OGN·249/298")]
    [InlineData("FND-249/298")]
    public async Task PlayingMightyOffersAChoiceBeforeExhaustingOrCalling(string legend)
    {
        var opened = await Open(legend);
        Assert.Contains("PLAYED", opened.State.PlayerZones["P1"].Base);
        Assert.Equal(3, opened.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount);
        Assert.Equal(["LEGEND"], choice.ContextObjectIds);
        Assert.DoesNotContain(opened.Events, e => e.Kind is "RUNES_CALLED" or "LEGEND_EXHAUSTED");
        if (Environment.GetEnvironmentVariable("RIFTBOUND_POWERFUL_PLAYED_DIAGNOSTICS") is { } folder) {
            Directory.CreateDirectory(folder);
            var raw = JsonSerializer.Serialize(opened.State);
            var restored = JsonSerializer.Deserialize<MatchState>(raw)!;
            var name = legend.Replace('/', '-');
            File.WriteAllText(Path.Combine(folder, name + "-roundtrip-before.json"), raw);
            File.WriteAllText(Path.Combine(folder, name + "-roundtrip-after.json"), JsonSerializer.Serialize(restored));
        }
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
    }

    [Theory]
    [InlineData("OGN·249/298", true)] [InlineData("OGN·249/298", false)]
    [InlineData("FND-249/298", true)] [InlineData("FND-249/298", false)]
    public async Task ChoiceCanBeDeclinedOrPaidBeforeTheResponseWindow(string legend, bool accept)
    {
        var opened = await Open(legend);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var selected = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : []));
        Assert.Equal(accept, selected.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, selected.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(selected.Events, e => e.Kind == "RUNES_CALLED");
        Restore(selected.State); ArmamentMightTests.PublicRecovery(selected.State);
        if (!accept) {
            Assert.Equal("PLAYED", Assert.Single(selected.State.StackItems).SourceObjectId);
            var own = await Top(selected.State);
            Assert.DoesNotContain(own.Events, e => e.Kind == "RUNES_CALLED");
            Restore(own.State); ArmamentMightTests.PublicRecovery(own.State); return;
        }
        Assert.Equal(2, selected.State.StackItems.Count);
        var resolved = await Top(selected.State);
        Assert.Equal(2, resolved.State.PlayerZones["P1"].RuneDeck.Count);
        var rune = Assert.Single(resolved.Events, e => e.Kind == "RUNES_CALLED");
        var id = Assert.Single(Assert.IsType<string[]>(rune.Payload["runeObjectIds"]));
        Assert.Contains(id, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[id].IsExhausted);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
        var ownResolved = await Top(resolved.State);
        Assert.Empty(ownResolved.State.StackItems);
        Restore(ownResolved.State); ArmamentMightTests.PublicRecovery(ownResolved.State);
    }

    [Theory]
    [InlineData("history")] [InlineData("source-generation")] [InlineData("unit-generation")]
    [InlineData("might")] [InlineData("queue-tick")]
    public async Task TriggerOrderRejectsForgedPlayEvidenceWithoutMutation(string field)
    {
        var played = await Act(Position("OGN·249/298"), "P1", new PlayCardCommand("PLAYED", "SFD·175/221", []));
        var state = played.State;
        var trigger = Assert.Single(state.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var context = Assert.IsType<MightySkillContext>(trigger.MightySkill);
        var forged = field switch {
            "source-generation" => trigger with { MightySkill = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } },
            "unit-generation" => trigger with { MightySkill = context with { Unit = context.Unit with { Generation = context.Unit.Generation + 1 } } },
            "might" => trigger with { MightySkill = context with { AfterMight = context.AfterMight + 1 } },
            "queue-tick" => trigger with { EnqueuedAtTick = state.Tick + 1 },
            _ => trigger };
        state = state with { TriggerQueue = state.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId ? forged : t).ToArray() };
        if (field == "history") state = state with { CardPlays = state.CardPlays with { Resolved = [] } };
        var command = new OrderTriggersCommand(state.TriggerQueue.Select(t => t.TriggerId).ToArray());
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-order", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static MatchState WithRunesAndWyverns(MatchState state, int available, int friendly, int enemy)
    {
        foreach (var (controller, copies, field) in new[] { ("P1", friendly, "F"), ("P2", enemy, "OTHER") })
            for (var i = 0; i < copies; i++)
                state = OfficialHoldSequenceTests.AddUnit(state, $"{controller}-WYVERN-{i}", "RAD·092/167", field, controller);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in state.PlayerZones["P1"].RuneDeck) { cards.Remove(id); locations.Remove(id); }
        var runes = Enumerable.Range(0, available).Select(i => $"REPLACEMENT-RUNE-{i}").ToArray();
        foreach (var id in runes) {
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", "RUNE_DECK");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { RuneDeck = runes } } };
    }

    [Theory]
    [InlineData("OGN·249/298", 3, 0, 0, 1)]
    [InlineData("OGN·249/298", 3, 1, 0, 2)]
    [InlineData("OGN·249/298", 3, 2, 0, 3)]
    [InlineData("OGN·249/298", 1, 2, 0, 1)]
    [InlineData("OGN·249/298", 0, 1, 0, 0)]
    [InlineData("OGN·249/298", 3, 0, 2, 1)]
    [InlineData("FND-249/298", 3, 1, 0, 2)]
    public async Task PaidLegendCallUsesTheSharedRuneReplacement(string legend, int available, int friendly, int enemy, int called)
    {
        var initial = WithRunesAndWyverns(Position(legend), available, friendly, enemy);
        var opened = await Open(legend, initial);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]));
        Assert.Equal(available, paid.State.PlayerZones["P1"].RuneDeck.Count);
        var resolved = await Top(paid.State);
        var ids = resolved.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("REPLACEMENT-RUNE-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(called, ids.Length);
        Assert.All(ids, id => { Assert.True(resolved.State.CardObjects[id].IsExhausted); Assert.Equal("BASE", resolved.State.ObjectLocations[id].Zone); });
        Assert.Equal(available - called, resolved.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(called, Convert.ToInt32(Assert.Single(resolved.Events, e => e.Kind == "RUNES_CALLED").Payload["count"]));
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BothSimultaneousPlaySkillsFollowTheChosenResolutionOrder(bool legendFirst)
    {
        var opened = await Open("OGN·249/298", legendFirst: legendFirst);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]));
        var first = await Top(paid.State);
        Assert.Equal(legendFirst ? 2 : 3, first.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(legendFirst, first.Events.Any(e => e.Kind == "RUNES_CALLED"));
        var second = await Top(first.State);
        Assert.Equal(2, second.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(!legendFirst, second.Events.Any(e => e.Kind == "RUNES_CALLED"));
        Assert.Empty(second.State.StackItems);
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
    }

    [Fact]
    public async Task ActualJournalReplaysEachConfirmationAndResponsePrefix()
    {
        var initial = WithRunesAndWyverns(Position("OGN·249/298"), 3, 1, 0);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        async Task Replay() => await BanishEffectPlayTests.Replay(initial, driver);
        await driver.Act("P1", new PlayCardCommand("PLAYED", "SFD·175/221", [])); await Replay();
        var legend = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var own = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "PLAYED");
        await driver.Act("P1", new OrderTriggersCommand([legend.TriggerId, own.TriggerId])); await Replay();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"])); await Replay();
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand()); await Replay();
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand()); await Replay();
        Assert.Single(driver.State.PlayerZones["P1"].RuneDeck);
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand()); await Replay();
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand()); await Replay();
        Assert.Empty(driver.State.StackItems);
    }

    [Fact]
    public async Task AReturnedPlayedUnitIsNotIdentifiedByThePendingPublicSkill()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position("OGN·249/298"), "RETURN", "OGN·104/298");
        var opened = await Open("OGN·249/298", initial);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]));
        var response = await Act(paid.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PLAYED"]));
        var returned = await Top(response.State);
        Assert.Contains("PLAYED", returned.State.PlayerZones["P1"].Hand);
        var opponent = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"]);
        var spectator = JsonSerializer.Serialize(MatchReplayRedactor.BuildSpectatorFrame(returned.State.RoomId, returned.State.Tick, 0, [], returned.State));
        Assert.DoesNotContain("\"PLAYED\"", opponent, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PLAYED\"", spectator, StringComparison.Ordinal);
        Restore(returned.State); ArmamentMightTests.PublicRecovery(returned.State);
        var resolved = await Top(returned.State);
        Assert.DoesNotContain(resolved.Events, e => e.Payload.Values.Any(value => Equals(value, "PLAYED")));
        Assert.Equal(2, resolved.State.PlayerZones["P1"].RuneDeck.Count);
    }

    [Theory]
    [InlineData("context")] [InlineData("legal")] [InlineData("count")]
    public async Task ForgedConfirmationMetadataCannotAuthorizeTheCost(string field)
    {
        var opened = await Open("OGN·249/298"); var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var forged = field switch {
            "context" => choice with { ContextObjectIds = ["PLAYED"] },
            "legal" => choice with { LegalObjectIds = ["PLAYED"] },
            _ => choice with { MaxCount = 2 } };
        var state = opened.State with { PendingCardChoice = forged };
        var command = new ChooseCardsCommand(forged.ChoiceId, forged.ChoiceWindow, ["LEGEND"]);
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-choice", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Theory]
    [InlineData("card")] [InlineData("confirmed")] [InlineData("extra")]
    public async Task RecoveryRejectsForgedPublicSkillProjection(string field)
    {
        var opened = await Open("OGN·249/298"); var state = opened.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        ArmamentMightTests.PublicRecovery(state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var payload = Assert.Single(snapshot.Stack.Select(item => Assert.IsType<Dictionary<string, object?>>(item)),
                item => Equals(item.GetValueOrDefault("effectKind"), "POWERFUL_PLAYED_LEGEND_CHANNEL"));
            var original = payload["powerfulPlayedRune"];
            var forged = new Dictionary<string, object?> { ["cardNo"] = field == "card" ? "FND-249/298" : "OGN·249/298",
                ["confirmed"] = field == "confirmed" };
            if (field == "extra") forged["privateObject"] = "PLAYED";
            payload["powerfulPlayedRune"] = forged;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            payload["powerfulPlayedRune"] = original;
        }
    }

    [Theory]
    [InlineData(false, 4)] [InlineData(true, 5)]
    public async Task EntryConditionUsesCurrentMightIncludingTheBattlefieldAura(bool bonus, int might)
    {
        var state = Position("OGN·249/298");
        var cards = state.CardObjects.ToDictionary(); cards.Remove("PLAYED");
        cards["F"] = cards["F"] with { CardNo = bonus ? "OGN·294/298" : "OGN·278/298" };
        var locations = state.ObjectLocations.ToDictionary(); locations.Remove("PLAYED");
        state = state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [] } } };
        state = MightyExperienceFrameTests.HandUnit(state, "PLAYED", "SFD·125/221");
        var played = await Act(state, "P1", new PlayCardCommand("PLAYED", "SFD·125/221", [], Destination: "BATTLEFIELD:F"));
        var actual = CurrentPowerTargetTests.Snapshot(played.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("PLAYED").GetProperty("effectivePower").GetInt32();
        Assert.Equal(might, actual);
        var skills = played.State.StackItems.Select(i => i.MightySkill).Concat(played.State.TriggerQueue.Select(t => t.MightySkill))
            .Where(c => c?.Kind == "POWERFUL_PLAYED_LEGEND_CHANNEL").ToArray();
        if (bonus) Assert.Equal(5, Assert.Single(skills)!.AfterMight);
        else Assert.Empty(skills);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }
}
