using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AkshanTemperedOrderingTests
{
    private const string Akshan = "SFD·109/221", Fee = "SPEND_POWER:2:ORANGE";

    private static MatchState Position(bool ownGear = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", Akshan, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P2", "BASE");
        if (ownGear) state = RadGearTestPosition.Put(state, "OWN-GEAR", "SFD·022/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < 12; i++)
            state = RadGearTestPosition.Put(state, "RUNE-" + i,
                i < 2 ? "UNL-R04" : i == 3 ? "UNL-R03" : "UNL-R01", "P1", "BASE");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = RunePool.Empty } };
    }

    private static async Task<Driver> Play(bool ownGear = true, bool paid = true, string destination = "BASE")
    {
        // Preserve the complete accepted resource and play history. Checkpoints
        // below explicitly cold-restore and replay it at the ordering boundary.
        var game = new Driver(Position(ownGear)) { RecoverAfterEveryCommand = false };
        await game.TapRunes("P1", Enumerable.Range(0, 12).Select(i => "RUNE-" + i));
        await game.Act("P1", new RecycleRuneCommand("RUNE-0", ["RUNE-0", "RUNE-1"]));
        await game.Act("P1", new RecycleRuneCommand("RUNE-2"));
        await game.Act("P1", new RecycleRuneCommand("RUNE-3"));
        Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [],
            OptionalCosts: paid ? [Fee] : [], Destination: destination));
        return game;
    }

    private static async Task OrderAndConfirm(Driver game, bool theftFirst,
        bool declineTheft = false, bool declineTempered = false)
    {
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var theft = Assert.Single(game.State.TriggerQueue, t => t.PermanentPlayStackItemId is not null);
        var tempered = Assert.Single(game.State.TriggerQueue, t => t.Tempered is not null);
        var order = theftFirst ? new[] { theft.TriggerId, tempered.TriggerId }
            : new[] { tempered.TriggerId, theft.TriggerId };
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.Contains(CommandTypes.OrderTriggers, game.PromptFor("P1").EnabledActions());
        Assert.DoesNotContain(CommandTypes.OrderTriggers, game.PromptFor("P2").EnabledActions());
        await RadGearTestPosition.Reject(game.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: order));
        await game.ColdCheckpoint();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        Assert.Empty(game.State.TriggerQueue);
        while (game.State.PendingCardChoice is { } choice)
        {
            var isTempered = choice.ChoiceWindow == "TEMPERED_CONFIRMATION";
            Assert.Contains(choice.ChoiceWindow, new[] { "TEMPERED_CONFIRMATION", "TRIGGER_CONFIRMATION" });
            var target = isTempered ? "OWN-GEAR" : "GEAR";
            Assert.Contains(target, choice.LegalObjectIds);
            var declined = isTempered ? declineTempered : declineTheft;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, declined ? [] : [target]));
        }
        Assert.Equal((declineTheft ? 0 : 1) + (declineTempered ? 0 : 1), game.State.StackItems.Count);
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_ORDERING_DEFERRED");
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false, "BASE")] [InlineData(true, "BASE")]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BATTLEFIELD:BF")]
    public async Task ActualPaidEntryWithoutOtherObserversLetsTheControllerChooseEitherResolutionOrder(
        bool theftFirst, string destination)
    {
        var game = await Play(destination: destination);
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        await OrderAndConfirm(game, theftFirst);
        Assert.Equal(theftFirst, game.State.StackItems[^1].SourceConfirmed);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        await game.Top();
        Assert.Single(game.State.StackItems);
        Assert.Equal(theftFirst ? "P1" : "P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal(theftFirst ? null : "UNIT", game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("UNIT", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("UNIT", game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Single(game.State.CardPlays.Resolved, p => p.CardNo == Akshan && p.PlayerId == "P1");
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task EitherOrderedSkillCanBeDeclinedIndependentlyWithoutRefundingThePaidOrange(
        bool theftFirst, bool declineTheft)
    {
        var game = await Play();
        await OrderAndConfirm(game, theftFirst, declineTheft, !declineTheft);
        await game.Top();
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        Assert.Equal(declineTheft ? "P2" : "P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal(declineTheft ? "UNIT" : null, game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ARealReturnResponseKeepsBothEarnedSkillsBoundToTheDepartedSource(bool theftFirst)
    {
        var game = await Play();
        var originalGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        await OrderAndConfirm(game, theftFirst);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.StackItems.Count);
        AssertDepartedSourcePrivacy(game);
        await game.ColdCheckpoint();
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(game.State.CardObjects["GEAR"].EquipmentControl);
        Assert.Null(game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, []));
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > originalGeneration);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(game.State.CardObjects["OWN-GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ASingleUsableSkillKeepsItsExistingConfirmationWithoutOrdering(bool paid)
    {
        var game = await Play(ownGear: !paid, paid: paid);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(paid ? 1 : 0, game.Events().Count(e => e.Event.Kind == "TRIGGER_ORDERING_DEFERRED"));
        Assert.DoesNotContain(CommandTypes.OrderTriggers, game.PromptFor("P1").EnabledActions());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(paid ? "TRIGGER_CONFIRMATION" : "TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(game.State.StackItems);
        Assert.Equal(paid ? 0 : 2, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("source")] [InlineData("card")] [InlineData("controller")] [InlineData("generation")]
    [InlineData("tick")] [InlineData("legacy-id")] [InlineData("missing-companion")] [InlineData("missing-deferred")] [InlineData("duplicate")]
    public async Task OriginalPlayHistoryRejectsForgedTemperedCompanionsAndDeferredReceipts(string mutation)
    {
        var game = await Play();
        var state = game.State;
        var legacy = Assert.Single(state.TriggerQueue, t => t.PermanentPlayStackItemId is not null);
        var tempered = Assert.Single(state.TriggerQueue, t => t.Tempered is not null);
        var context = Assert.IsType<TemperedContext>(tempered.Tempered);
        var altered = mutation switch {
            "source" => tempered with { SourceObjectId = "ALLY" },
            "card" => tempered with { Tempered = context with { CardNo = "SFD·085/221" } },
            "controller" => tempered with { ControllerId = "P2" },
            "generation" => tempered with { Tempered = context with {
                Unit = context.Unit with { Generation = context.Unit.Generation - 1 } } },
            "tick" => legacy with { EnqueuedAtTick = legacy.EnqueuedAtTick - 1 },
            "legacy-id" => legacy with { PermanentPlayStackItemId = "unrelated-accepted-play" },
            _ => tempered
        };
        var forged = state with { TriggerQueue = mutation switch {
            "missing-companion" => state.TriggerQueue.Where(t => t.TriggerId != tempered.TriggerId).ToArray(),
            "missing-deferred" => state.TriggerQueue.Where(t => t.TriggerId != legacy.TriggerId).ToArray(),
            "duplicate" => [.. state.TriggerQueue, legacy with { TriggerId = legacy.TriggerId + "-duplicate" }],
            _ => state.TriggerQueue.Select(t => t.TriggerId == altered.TriggerId ? altered : t).ToArray()
        } };
        var journal = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Empty(Errors(game, state));
        Assert.NotEmpty(Errors(game, forged));
        Assert.Equal(journal, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    private static void AssertDepartedSourcePrivacy(Driver game)
    {
        var state = game.State;
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        foreach (var snapshot in new[] { snapshots["P2"], spectator.SpectatorSnapshot })
            foreach (var value in snapshot.Stack)
            {
                var row = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);
                Assert.Equal("HIDDEN", row["sourceObjectId"]);
                Assert.Equal("HIDDEN", row["sourceVisibility"]);
                Assert.Equal(Akshan, row["cardNo"]);
                Assert.DoesNotContain("tempered", row.Keys);
            }
        foreach (var value in snapshots["P1"].Stack)
            Assert.Equal("UNIT", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value)["sourceObjectId"]);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p,
            state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(),
            views, state, state.Tick, spectator));
        var forgedRows = spectator.SpectatorSnapshot.Stack.Select(value => (object?)new Dictionary<string, object?>(
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value)) {
                ["sourceObjectId"] = "UNIT", ["sourceVisibility"] = "VISIBLE" }).ToArray();
        var forged = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Stack = forgedRows } };
        Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(),
            views, state, state.Tick, forged));
    }

    internal static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p,
            state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }
}
