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
    private const string FizzTokenSpellObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-TOKEN-SPELL";
    private const string FizzTargetedRuneSpellObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-TARGETED-RUNE-SPELL";
    private const string FizzTargetedCalledRuneObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-TARGETED-CALLED-RUNE";
    private const string FizzExtraFriendlyUnitObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-EXTRA-FRIENDLY-UNIT";
    private const string FizzCopyTokenSpellObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-COPY-TOKEN-SPELL";
    private const string FizzExtraCopyTokenTargetObjectId = "P1-SOURCE-UNIT-PLAYED-FIZZ-EXTRA-COPY-TOKEN-TARGET";

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

    [Fact]
    public async Task FizzPlaysNoTargetGraveyardTokenSpellAndRecyclesItAfterSourceUnitPlayed()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzGraveyardTokenSpellState(),
            "intent-fizz-token-spell");

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzTokenSpellObjectId, sourceTrigger.Payload["targetObjectId"]);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, sourceTrigger.Payload["reason"]);

        var playEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, FizzTokenSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·140/221", playEvent.Payload["sourceCardNo"]);
        Assert.Equal("OGN·094/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(3, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.False(playEvent.Payload.ContainsKey("targetObjectIds"));

        var tokenEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzTokenSpellObjectId, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("精灵", tokenEvent.Payload["tokenName"]);
        Assert.Equal(3, tokenEvent.Payload["power"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        Assert.Contains("瞬息", Assert.IsType<string[]>(tokenEvent.Payload["tokenTags"]));

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzTokenSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([FizzObjectId, tokenObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(tokenObjectId, p2Pass.State.CardObjects);
        Assert.Contains("瞬息", p2Pass.State.CardObjects[tokenObjectId].Tags);
        Assert.Equal(3, p2Pass.State.CardObjects[tokenObjectId].Power);
        Assert.DoesNotContain(FizzTokenSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzTokenSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[tokenObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzTokenSpellObjectId].Zone);
    }

    [Fact]
    public async Task FizzPlaysTargetedGraveyardRuneSpellAndRecyclesItAfterSourceUnitPlayed()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzTargetedGraveyardRuneSpellState(extraFriendlyUnitTarget: false),
            "intent-fizz-targeted-rune-spell");

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzTargetedRuneSpellObjectId, sourceTrigger.Payload["targetObjectId"]);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, sourceTrigger.Payload["reason"]);

        var playEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, FizzTargetedRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·140/221", playEvent.Payload["sourceCardNo"]);
        Assert.Equal("OGN·104/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(1, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(playEvent.Payload["ignorePlayManaCost"]));
        Assert.True(Assert.IsType<bool>(playEvent.Payload["payPlayPowerCosts"]));
        Assert.Equal([FizzObjectId], Assert.IsType<string[]>(playEvent.Payload["targetObjectIds"]));

        var returnedEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzTargetedRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, returnedEvent.Payload["targetObjectId"]);
        Assert.Equal("P1", returnedEvent.Payload["ownerPlayerId"]);

        var runeEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzTargetedRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal("P1", runeEvent.Payload["playerId"]);
        Assert.Equal(1, runeEvent.Payload["count"]);
        Assert.Equal([FizzTargetedCalledRuneObjectId], Assert.IsType<string[]>(runeEvent.Payload["runeObjectIds"]));

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzTargetedRuneSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Contains(FizzObjectId, p2Pass.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(FizzObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal([FizzTargetedCalledRuneObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(FizzTargetedCalledRuneObjectId, p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.True(p2Pass.State.CardObjects[FizzTargetedCalledRuneObjectId].IsExhausted);
        Assert.DoesNotContain(FizzTargetedRuneSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzTargetedRuneSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Hand, p2Pass.State.ObjectLocations[FizzObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzTargetedRuneSpellObjectId].Zone);
    }

    [Fact]
    public async Task FizzDoesNotAutoSelectTargetedGraveyardRuneSpellWhenMultipleFriendlyTargetsAreLegal()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzTargetedGraveyardRuneSpellState(extraFriendlyUnitTarget: true),
            "intent-fizz-targeted-rune-spell-multiple-targets");

        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzTargetedRuneSpellObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));

        Assert.Contains(FizzObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(FizzExtraFriendlyUnitObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(FizzObjectId, p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Contains(FizzTargetedRuneSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(FizzTargetedCalledRuneObjectId, p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[FizzObjectId].Zone);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[FizzExtraFriendlyUnitObjectId].Zone);
        Assert.Equal(TriggerZones.Graveyard, p2Pass.State.ObjectLocations[FizzTargetedRuneSpellObjectId].Zone);
    }

    [Fact]
    public async Task FizzPlaysCopyTargetGraveyardTokenSpellWhenExactlyOneUnitTargetIsLegal()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzCopyTargetGraveyardTokenSpellState(extraUnitTarget: false),
            "intent-fizz-copy-token-spell");

        var sourceTrigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, sourceTrigger.Payload["effectId"]);
        Assert.Equal(FizzCopyTokenSpellObjectId, sourceTrigger.Payload["targetObjectId"]);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, sourceTrigger.Payload["reason"]);

        var playEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, FizzCopyTokenSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(FizzObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·140/221", playEvent.Payload["sourceCardNo"]);
        Assert.Equal("UNL-200/219", playEvent.Payload["playedCardNo"]);
        Assert.Equal(3, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.Equal([FizzObjectId], Assert.IsType<string[]>(playEvent.Payload["targetObjectIds"]));

        var tokenEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzCopyTokenSpellObjectId, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("映像", tokenEvent.Payload["tokenName"]);
        Assert.Equal(3, tokenEvent.Payload["power"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        Assert.Equal(FizzObjectId, tokenEvent.Payload["copiedTargetObjectId"]);
        Assert.Equal("SFD·140/221", tokenEvent.Payload["copiedCardNo"]);
        Assert.Equal("SFD·140/221", tokenEvent.Payload["tokenCardNo"]);
        var tokenTags = Assert.IsType<string[]>(tokenEvent.Payload["tokenTags"]);
        Assert.Contains(CardObjectTags.Ephemeral, tokenTags);
        Assert.Contains("映像", tokenTags);
        Assert.Contains("约德尔人", tokenTags);

        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.Equal([FizzCopyTokenSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([FizzObjectId, tokenObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("SFD·140/221", p2Pass.State.CardObjects[tokenObjectId].CardNo);
        Assert.Equal(3, p2Pass.State.CardObjects[tokenObjectId].Power);
        Assert.Contains(CardObjectTags.Ephemeral, p2Pass.State.CardObjects[tokenObjectId].Tags);
        Assert.Contains("映像", p2Pass.State.CardObjects[tokenObjectId].Tags);
        Assert.Contains("约德尔人", p2Pass.State.CardObjects[tokenObjectId].Tags);
        Assert.DoesNotContain(FizzCopyTokenSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([FizzCopyTokenSpellObjectId], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[tokenObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, p2Pass.State.ObjectLocations[FizzCopyTokenSpellObjectId].Zone);
    }

    [Fact]
    public async Task FizzDoesNotAutoSelectCopyTargetGraveyardTokenSpellWhenMultipleUnitTargetsAreLegal()
    {
        var p2Pass = await ResolveFizzPlayThroughStackAsync(
            BuildFizzCopyTargetGraveyardTokenSpellState(extraUnitTarget: true),
            "intent-fizz-copy-token-spell-multiple-targets");

        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzCopyTokenSpellObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FizzObjectId, StringComparison.Ordinal));

        Assert.Contains(FizzObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(FizzExtraCopyTokenTargetObjectId, p2Pass.State.PlayerZones["P2"].Base);
        Assert.Contains(FizzCopyTokenSpellObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[FizzObjectId].Zone);
        Assert.Equal(TriggerZones.Base, p2Pass.State.ObjectLocations[FizzExtraCopyTokenTargetObjectId].Zone);
        Assert.Equal(TriggerZones.Graveyard, p2Pass.State.ObjectLocations[FizzCopyTokenSpellObjectId].Zone);
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

    private static MatchState BuildFizzGraveyardTokenSpellState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = Unit(FizzObjectId, "P1", 3, "SFD·140/221", ["约德尔人"]),
            [FizzTokenSpellObjectId] = Spell(FizzTokenSpellObjectId, "P1", "OGN·094/298", 3)
        };

        return new MatchState(
            roomId: "source-unit-played-fizz-graveyard-token-spell-room",
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
                    Graveyard = [FizzTokenSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [FizzObjectId] = new("P1", TriggerZones.Hand),
                [FizzTokenSpellObjectId] = new("P1", TriggerZones.Graveyard)
            });
    }

    private static MatchState BuildFizzTargetedGraveyardRuneSpellState(bool extraFriendlyUnitTarget)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = Unit(FizzObjectId, "P1", 3, "SFD·140/221", ["约德尔人"]),
            [FizzTargetedRuneSpellObjectId] = Spell(FizzTargetedRuneSpellObjectId, "P1", "OGN·104/298", 1),
            [FizzTargetedCalledRuneObjectId] = Rune(FizzTargetedCalledRuneObjectId, "P1")
        };
        var p1Base = Array.Empty<string>();
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = new("P1", TriggerZones.Hand),
            [FizzTargetedRuneSpellObjectId] = new("P1", TriggerZones.Graveyard),
            [FizzTargetedCalledRuneObjectId] = new("P1", "RUNE_DECK")
        };
        if (extraFriendlyUnitTarget)
        {
            cardObjects[FizzExtraFriendlyUnitObjectId] = Unit(FizzExtraFriendlyUnitObjectId, "P1", 2, "SFD·125/221", []);
            p1Base = [FizzExtraFriendlyUnitObjectId];
            objectLocations[FizzExtraFriendlyUnitObjectId] = new("P1", TriggerZones.Base);
        }

        return new MatchState(
            roomId: "source-unit-played-fizz-targeted-graveyard-rune-spell-room",
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
                    Base = p1Base,
                    Hand = [FizzObjectId],
                    Graveyard = [FizzTargetedRuneSpellObjectId],
                    RuneDeck = [FizzTargetedCalledRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static MatchState BuildFizzCopyTargetGraveyardTokenSpellState(bool extraUnitTarget)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = Unit(FizzObjectId, "P1", 3, "SFD·140/221", ["约德尔人"]),
            [FizzCopyTokenSpellObjectId] = Spell(FizzCopyTokenSpellObjectId, "P1", "UNL-200/219", 3)
        };
        var p2Base = Array.Empty<string>();
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [FizzObjectId] = new("P1", TriggerZones.Hand),
            [FizzCopyTokenSpellObjectId] = new("P1", TriggerZones.Graveyard)
        };
        if (extraUnitTarget)
        {
            cardObjects[FizzExtraCopyTokenTargetObjectId] = Unit(
                FizzExtraCopyTokenTargetObjectId,
                "P2",
                4,
                "SFD·068/221",
                ["机械"]);
            p2Base = [FizzExtraCopyTokenTargetObjectId];
            objectLocations[FizzExtraCopyTokenTargetObjectId] = new("P2", TriggerZones.Base);
        }

        return new MatchState(
            roomId: "source-unit-played-fizz-copy-target-graveyard-token-spell-room",
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
                    Graveyard = [FizzCopyTokenSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
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
