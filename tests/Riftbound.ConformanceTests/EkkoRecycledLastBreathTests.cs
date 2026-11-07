using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EkkoRecycledLastBreathTests
{
    private const string Ekko = "OGN·110/298", Kill = "OGN·229/298";

    private static MatchState Position(int runes = 2)
    {
        // Explicit turn-five materials, not a claim of a canonical opening.
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", Ekko, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "OTHER-GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        for (var i = 0; i < runes; i++) state = RadGearTestPosition.Put(state, "R" + i, "UNL-R03", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ENEMY-RUNE", "UNL-R06", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "RUNE-DECK", "UNL-R03", "P1", "RUNE_DECK");
        return state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p =>
            p.Key == "ENEMY-RUNE" || Enumerable.Range(0, runes).Any(i => p.Key == "R" + i)
                ? p.Value with { IsExhausted = true } : p.Value) };
    }

    private static async Task<Driver> Die(MatchState initial, bool play = true, bool song = false)
    {
        var game = new Driver(initial);
        if (play) await game.Act("P1", new PlayCardCommand("UNIT", Ekko, []));
        if (song)
        {
            await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"]));
            await game.Top();
            Assert.Equal(Ekko, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        }
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"]));
        await game.Top();
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        return game;
    }

    private static ChooseCardsCommand Cost(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.TriggerCostWindow, choice.ChoiceWindow);
        Assert.Equal(new[] { "UNIT" }, choice.LegalObjectIds);
        return new(choice.ChoiceId, choice.ChoiceWindow, ids);
    }

    [Theory, InlineData(0), InlineData(2), InlineData(8)]
    public async Task ARealDeathRecyclesAtConfirmationAndReadiesOnlyControlledFieldRunesAfterResponses(int runes)
    {
        var game = await Die(Position(runes));
        var skill = Assert.Single(game.State.StackItems);
        var birth = Assert.IsType<LastBreathRuneCallContext>(skill.LastBreathRuneCall);
        Assert.Equal(TriggerKinds.UnitLastBreathRecycleSelfReadyAllRunes, skill.EffectKind);
        Assert.Null(skill.TriggerCost); Assert.Empty(skill.TargetObjectIds);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(birth.Source.Generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNE_READIED");
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        await game.Act("P1", Cost(game, "UNIT"));
        var paid = Assert.Single(game.State.StackItems);
        Assert.True(paid.TriggerCost!.SourceWasRecycled);
        Assert.Equal(new ObjectBinding("UNIT", birth.Source.Generation + 1), paid.TriggerCost.Source);
        Assert.Equal(birth.Source.Generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal("UNIT", game.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.All(Enumerable.Range(0, runes), i => Assert.True(game.State.CardObjects["R" + i].IsExhausted));
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        var prefix = game.Events().Length;
        await game.Top();
        Assert.All(Enumerable.Range(0, runes), i => Assert.False(game.State.CardObjects["R" + i].IsExhausted));
        Assert.True(game.State.CardObjects["ENEMY-RUNE"].IsExhausted);
        Assert.Contains("RUNE-DECK", game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(runes, game.Events().Skip(prefix).Count(e => e.Event.Kind == "RUNE_READIED"));
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task RejectingTheCostOrChoosingADifferentCardCannotRecycleOrReadyAnything()
    {
        var game = await Die(Position());
        foreach (var id in new[] { "OTHER-GRAVE", "ALLY", "ENEMY", "R0" })
            await RadGearTestPosition.Reject(game.State, "P1", Cost(game, id));
        await RadGearTestPosition.Reject(game.State, "P2", Cost(game, "UNIT"));
        await game.Act("P1", Cost(game));
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["R0"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARDS_RECYCLED" or "RUNE_READIED");
        await game.Replay();
    }

    [Fact]
    public async Task ARealResponseDrawsTheRecycledSourceAndTheEarnedSkillAlsoReadiesTheNewlyCalledRune()
    {
        var initial = RadGearTestPosition.Put(Position(), "DRAW", "RAD·029/167", "P1", "HAND");
        var moved = initial.PlayerZones["P1"].MainDeck.ToArray();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Banished = [..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key,
                p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "BANISHED") : p.Value) };
        var game = await Die(initial);
        await game.Act("P1", Cost(game, "UNIT"));
        var earned = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("DRAW", "RAD·029/167", [],
            OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:ALLY"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("RUNE-DECK", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["RUNE-DECK"].IsExhausted);
        Assert.Equal(earned, Assert.Single(game.State.StackItems).StackItemId);
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        var handSource = MatchStateHasher.HashValue(game.State.CardObjects["UNIT"]);
        await game.Top();
        Assert.False(game.State.CardObjects["RUNE-DECK"].IsExhausted);
        Assert.False(game.State.CardObjects["R0"].IsExhausted);
        Assert.True(game.State.CardObjects["ENEMY-RUNE"].IsExhausted);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Equal(handSource, MatchStateHasher.HashValue(game.State.CardObjects["UNIT"]));
        await game.Replay();
    }

    [Fact]
    public async Task AnotherOwnersSourceReturnsToItsDeckButOnlyTheSkillControllerReadiesRunes()
    {
        var initial = RadGearTestPosition.Put(Position(), "UNIT", Ekko, "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2" } } };
        initial = RadGearTestPosition.Put(initial, "KARMA-1", "OGN·235/298", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "KARMA-2", "OGN·235/298", "P2", "BASE");
        var game = await Die(initial, play: false);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        Assert.Contains("UNIT", game.State.PlayerZones["P2"].Graveyard);
        await game.Act("P1", Cost(game, "UNIT"));
        Assert.Equal("UNIT", game.State.PlayerZones["P2"].MainDeck[^1]);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == "RECYCLE_BOON");
        await game.Top();
        Assert.False(game.State.CardObjects["R0"].IsExhausted);
        Assert.True(game.State.CardObjects["ENEMY-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task TwoRealSongLastBreathsCanOnlyPayWithTheSameCorpseOnce()
    {
        var initial = RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        var game = await Die(initial, song: true);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(2, game.State.StackItems.Select(i => i.LastBreathRuneCall!.InstanceId).Distinct().Count());
        await game.Act("P1", Cost(game, "UNIT"));
        Assert.Single(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        await game.Top();
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "RUNE_READIED"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ARealImageOfEkkoCannotRecycleItsCeasedTokenToPayTheLeadingCost()
    {
        var initial = RadGearTestPosition.Put(Position(), "ENEMY", Ekko, "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ENEMY"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Ekko, image.CardNo);
        await game.Act("P1", new PlayCardCommand("KILL", Kill, [image.ObjectId])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARDS_RECYCLED" or "RUNE_READIED");
        Assert.True(game.State.CardObjects["R0"].IsExhausted); await game.Replay();
    }

    [Theory, InlineData("missing"), InlineData("flag"), InlineData("field-generation"), InlineData("future"), InlineData("power")]
    public async Task RecoveryRejectsForgedOrMissingRecycleReceipts(string mutation)
    {
        var game = await Die(Position()); await game.Act("P1", Cost(game, "UNIT"));
        var skill = Assert.Single(game.State.StackItems); var paid = skill.TriggerCost!;
        TriggerCostReceipt? forged = mutation switch {
            "missing" => null,
            "flag" => paid with { SourceWasRecycled = false },
            "field-generation" => paid with { Source = paid.Source with { Generation = paid.Source.Generation - 1 } },
            "future" => paid with { Source = paid.Source with { Generation = paid.Source.Generation + 1 } },
            _ => paid with { Power = 1 }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [skill with { TriggerCost = forged }] }));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("recycle"), InlineData("generation"), InlineData("confirm"), InlineData("command")]
    public async Task RecoveryBindsThePaidReceiptToTheOriginalAcceptedRecycleCommandAndEvents(string mutation)
    {
        var game = await Die(Position()); await game.Act("P1", Cost(game, "UNIT"));
        var original = game.Events(); var commands = game.Commands();
        var recycle = Assert.Single(original, e => e.Event.Kind == "CARDS_RECYCLED");
        var events = mutation switch {
            "recycle" => original.Where(e => e != recycle).ToArray(),
            "generation" => original.Select(e => e != recycle ? e : e with { Event = e.Event with {
                Payload = new Dictionary<string, object?>(e.Event.Payload) { ["sourceGeneration"] = 999L } } }).ToArray(),
            "confirm" => original.Where(e => e.Event.Kind != "TRIGGER_CONFIRMED").ToArray(),
            _ => original
        };
        if (mutation == "command") commands = commands.Where(c => c.CommandType != CommandTypes.ChooseCards).ToArray();
        var sequence = original[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
        var views = game.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p,
            game.State.Tick, sequence, snapshots[p], game.State.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
        var errors = MatchRecoveryValidator.Validate(game.State.RoomId, sequence, commands, events, views,
            game.State, game.State.Tick, spectator);
        Assert.Contains(errors, error => error.Contains("last-breath recycle receipt"));
        await game.ColdCheckpoint();
    }
}
