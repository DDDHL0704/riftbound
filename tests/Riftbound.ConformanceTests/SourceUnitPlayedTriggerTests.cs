using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SourceUnitPlayedTriggerTests
{
    private const string FizzObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ";
    private const string FizzSpellObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-SPELL";
    private const string FizzSpellDrawObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-SPELL-DRAW";
    private const string FizzRuneSpellObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-RUNE-SPELL";
    private const string FizzCalledRuneObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-CALLED-RUNE";
    private const string FizzRuneFallbackDrawObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-RUNE-FALLBACK-DRAW";

    [Fact]
    public async Task FizzPlaysLowCostGraveyardSpellAndRecyclesItAfterSourceUnitPlayed()
    {
        var engine = new CoreRuleEngine();
        var played = await engine.ResolveAsync(
            BuildFizzGraveyardSpellState(),
            new PlayerIntent("intent-fizz-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(FizzObjectId, "SFD·140/221", []),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Single(played.State.StackItems);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-fizz-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-fizz-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzSpellObjectId, sourceTrigger.Payload["targetObjectId"]);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, sourceTrigger.Payload["reason"]);

        var playEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, FizzSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·140/221", playEvent.Payload["sourceCardNo"]);
        Assert.Equal("OGN·048/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(2, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(playEvent.Payload["ignorePlayManaCost"]));
        Assert.True(Assert.IsType<bool>(playEvent.Payload["payPlayPowerCosts"]));

        var drawEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([FizzObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal([FizzSpellDrawObjectId], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(FizzSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzSpellObjectId].Zone);

        var fizzUnit = p2Pass.State.CardObjects[FizzObjectId];
        Assert.Equal("SFD·140/221", fizzUnit.CardNo);
        Assert.Equal(3, fizzUnit.Power);
        Assert.Contains(CardObjectTags.UnitCard, fizzUnit.Tags);
        Assert.Contains("约德尔人", fizzUnit.Tags);
    }

    [Fact]
    public async Task FizzPlaysLowCostGraveyardRuneSpellAndRecyclesItAfterSourceUnitPlayed()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzGraveyardRuneSpellState(runeDeckAvailable: true),
            "intent-fizz-rune-spell");

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzRuneSpellObjectId, sourceTrigger.Payload["targetObjectId"]);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, sourceTrigger.Payload["reason"]);

        var playEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, FizzRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·140/221", playEvent.Payload["sourceCardNo"]);
        Assert.Equal("OGN·134/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(2, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(playEvent.Payload["ignorePlayManaCost"]));
        Assert.True(Assert.IsType<bool>(playEvent.Payload["payPlayPowerCosts"]));

        var runeEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal("P1", runeEvent.Payload["playerId"]);
        Assert.Equal(1, runeEvent.Payload["count"]);
        Assert.Equal([FizzCalledRuneObjectId], Assert.IsType<string[]>(runeEvent.Payload["runeObjectIds"]));

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzRuneSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([FizzObjectId, FizzCalledRuneObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(FizzCalledRuneObjectId, p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.True(p2Pass.State.CardObjects[FizzCalledRuneObjectId].IsExhausted);
        Assert.DoesNotContain(FizzRuneSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzRuneSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzRuneSpellObjectId].Zone);
    }

    [Fact]
    public async Task FizzGraveyardRuneSpellDrawsAndRecyclesWhenRuneCallFailsAfterSourceUnitPlayed()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzGraveyardRuneSpellState(runeDeckAvailable: false),
            "intent-fizz-rune-spell-fallback-draw");

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzRuneSpellObjectId, sourceTrigger.Payload["targetObjectId"]);

        var runeEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(0, runeEvent.Payload["count"]);
        Assert.Empty(Assert.IsType<string[]>(runeEvent.Payload["runeObjectIds"]));

        var drawEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzRuneSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([FizzObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal([FizzRuneFallbackDrawObjectId], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(FizzRuneSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzRuneSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzRuneSpellObjectId].Zone);
    }

    private static MatchState BuildFizzGraveyardSpellState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = Unit(FizzObjectId, "P1", 3, "SFD·140/221", ["约德尔人"]),
            [FizzSpellObjectId] = Spell(FizzSpellObjectId, "P1", "OGN·048/298", 2),
            [FizzSpellDrawObjectId] = Unit(FizzSpellDrawObjectId, "P1", 2, "SFD·125/221", [])
        };

        return new MatchState(
            roomId: "source-unit-played-fizz-graveyard-spell-room",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [FizzObjectId],
                    MainDeck = [FizzSpellDrawObjectId],
                    Graveyard = [FizzSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [FizzObjectId] = new("P1", TriggerZones.Hand),
                [FizzSpellObjectId] = new("P1", TriggerZones.Graveyard),
                [FizzSpellDrawObjectId] = new("P1", TriggerZones.MainDeck)
            });
    }

    private static MatchState BuildFizzGraveyardRuneSpellState(bool runeDeckAvailable)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = Unit(FizzObjectId, "P1", 3, "SFD·140/221", ["约德尔人"]),
            [FizzRuneSpellObjectId] = Spell(FizzRuneSpellObjectId, "P1", "OGN·134/298", 2)
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = new("P1", TriggerZones.Hand),
            [FizzRuneSpellObjectId] = new("P1", TriggerZones.Graveyard)
        };
        if (runeDeckAvailable)
        {
            cardObjects[FizzCalledRuneObjectId] = Rune(FizzCalledRuneObjectId, "P1");
            objectLocations[FizzCalledRuneObjectId] = new("P1", "RUNE_DECK");
        }
        else
        {
            cardObjects[FizzRuneFallbackDrawObjectId] = Unit(
                FizzRuneFallbackDrawObjectId,
                "P1",
                2,
                "SFD·125/221",
                []);
            objectLocations[FizzRuneFallbackDrawObjectId] = new("P1", TriggerZones.MainDeck);
        }

        return new MatchState(
            roomId: "source-unit-played-fizz-graveyard-rune-spell-room",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [FizzObjectId],
                    MainDeck = runeDeckAvailable ? [] : [FizzRuneFallbackDrawObjectId],
                    Graveyard = [FizzRuneSpellObjectId],
                    RuneDeck = runeDeckAvailable ? [FizzCalledRuneObjectId] : []
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static async Task<ResolutionResult> ResolveFizzPlayThroughStackAsync(
        MatchState state,
        string intentPrefix)
    {
        var engine = new CoreRuleEngine();
        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent($"{intentPrefix}-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(FizzObjectId, "SFD·140/221", []),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Single(played.State.StackItems);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent($"{intentPrefix}-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"{intentPrefix}-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        return p2Pass;
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo,
        IReadOnlyList<string> additionalTags)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard, .. additionalTags],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Spell(
        string objectId,
        string playerId,
        string cardNo,
        int manaCost)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.SpellCard],
            manaCost: manaCost,
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Rune(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.RuneCard],
            ownerId: playerId,
            controllerId: playerId);
    }
}
