using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NaturalUnitConquestTriggerTests
{
    private const string TriggerPaymentWindow = "TRIGGER_PAYMENT";
    private const string PayTwoMana = "SPEND_MANA:2";
    private const string DeclinePayment = "DECLINE";
    private const string BattlefieldId = "P1-NATURAL-UNIT-CONQUEST-BATTLEFIELD";
    private const string KaisaObjectId = "P1-NATURAL-CONQUEST-KAISA";
    private const string KaisaSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-SPELL";
    private const string KaisaSpellDrawObjectId = "P1-NATURAL-CONQUEST-KAISA-SPELL-DRAW";
    private const string KaisaRuneSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-RUNE-SPELL";
    private const string KaisaCalledRuneObjectId = "P1-NATURAL-CONQUEST-KAISA-CALLED-RUNE";
    private const string KaisaRuneFallbackDrawObjectId = "P1-NATURAL-CONQUEST-KAISA-RUNE-FALLBACK-DRAW";
    private const string KaisaTokenSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-TOKEN-SPELL";
    private const string KaisaTokenDrawSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-TOKEN-DRAW-SPELL";
    private const string KaisaTokenDrawSpellDrawObjectId = "P1-NATURAL-CONQUEST-KAISA-TOKEN-DRAW-DRAW";
    private const string KaisaCopyTokenSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-COPY-TOKEN-SPELL";
    private const string KaisaExtraCopyTokenTargetObjectId = "P2-NATURAL-CONQUEST-KAISA-EXTRA-COPY-TOKEN-TARGET";
    private const string RumbleObjectId = "P1-NATURAL-CONQUEST-RUMBLE";
    private const string RumbleRecycledUnitObjectId = "P1-NATURAL-CONQUEST-RUMBLE-RECYCLED-UNIT";
    private const string RumbleGraveyardMechanicalUnitObjectId = "P1-NATURAL-CONQUEST-RUMBLE-GRAVEYARD-MECH";
    private const string TreantObjectId = "P1-NATURAL-CONQUEST-TREANT";
    private const string YetiObjectId = "P1-NATURAL-CONQUEST-YETI";
    private const string TryndamereObjectId = "P1-NATURAL-CONQUEST-TRYNDAMERE";
    private const string DefenderObjectId = "P2-NATURAL-CONQUEST-DEFENDER";
    private const string DrawObjectId = "P1-NATURAL-CONQUEST-DRAW";

    [Fact]
    public void UnitConquestTriggerRoutingEnumeratesBehaviorSpecTriggersInsteadOfEffectHelperAllowList()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var unitConquestRulesPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "UnitConquestTriggerSpecRules.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);
        var unitConquestRulesSource = File.ReadAllText(unitConquestRulesPath);

        Assert.DoesNotContain("UnitConquestTriggerSpecRules.TryGetUnitConquest", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public static bool TryGetUnitConquest", unitConquestRulesSource, StringComparison.Ordinal);
        Assert.Contains("UnitConquestTriggerSpecRules.TriggersForCard", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KaisaDrawsFromUnitConquestTriggerAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestState(),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-draw", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));

        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestDrawOne, conquestTrigger.Payload["effectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var drawEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);
        Assert.Equal([DrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task KaisaPlaysLowCostGraveyardSpellAndRecyclesItAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardSpellState(),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-spell", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaSpellObjectId, []);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaSpellObjectId, conquestTrigger.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, KaisaSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(KaisaObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·048/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(playEvent.Payload["ignorePlayManaCost"]));
        Assert.True(Assert.IsType<bool>(playEvent.Payload["payPlayPowerCosts"]));

        var drawEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([KaisaSpellDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(KaisaSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaPlaysLowCostGraveyardRuneSpellAndRecyclesItAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardRuneSpellState(runeDeckAvailable: true),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-rune-spell", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaRuneSpellObjectId, []);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaRuneSpellObjectId, conquestTrigger.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, KaisaRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(KaisaObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·134/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(playEvent.Payload["ignorePlayManaCost"]));
        Assert.True(Assert.IsType<bool>(playEvent.Payload["payPlayPowerCosts"]));

        var runeEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal("P1", runeEvent.Payload["playerId"]);
        Assert.Equal(1, runeEvent.Payload["count"]);
        Assert.Equal([KaisaCalledRuneObjectId], Assert.IsType<string[]>(runeEvent.Payload["runeObjectIds"]));

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaRuneSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Contains(KaisaCalledRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(KaisaCalledRuneObjectId, result.State.PlayerZones["P1"].RuneDeck);
        Assert.True(result.State.CardObjects[KaisaCalledRuneObjectId].IsExhausted);
        Assert.DoesNotContain(KaisaRuneSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaRuneSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaRuneSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaGraveyardRuneSpellDrawsAndRecyclesWhenRuneCallFailsAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardRuneSpellState(runeDeckAvailable: false),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-rune-spell-fallback-draw", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaRuneSpellObjectId, []);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaRuneSpellObjectId, conquestTrigger.Payload["targetObjectId"]);

        var runeEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaRuneSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(0, runeEvent.Payload["count"]);
        Assert.Empty(Assert.IsType<string[]>(runeEvent.Payload["runeObjectIds"]));

        var drawEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaRuneSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Equal([KaisaRuneFallbackDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(KaisaRuneSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaRuneSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaRuneSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaPlaysLowCostGraveyardTokenSpellAndRecyclesItAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardTokenSpellState(),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-token-spell", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaTokenSpellObjectId, []);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaTokenSpellObjectId, conquestTrigger.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, KaisaTokenSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(KaisaObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·094/298", playEvent.Payload["playedCardNo"]);
        Assert.Equal(3, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.False(playEvent.Payload.ContainsKey("targetObjectIds"));

        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaTokenSpellObjectId, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("精灵", tokenEvent.Payload["tokenName"]);
        Assert.Equal(3, tokenEvent.Payload["power"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        Assert.Contains("瞬息", Assert.IsType<string[]>(tokenEvent.Payload["tokenTags"]));

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaTokenSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(tokenObjectId, result.State.CardObjects);
        Assert.Contains("瞬息", result.State.CardObjects[tokenObjectId].Tags);
        Assert.Equal(3, result.State.CardObjects[tokenObjectId].Power);
        Assert.DoesNotContain(KaisaTokenSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaTokenSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, result.State.ObjectLocations[tokenObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaTokenSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaPlaysLowCostGraveyardTokenDrawSpellAndRecyclesItAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardTokenDrawSpellState(),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-token-draw-spell", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaTokenDrawSpellObjectId, []);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaTokenDrawSpellObjectId, conquestTrigger.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, KaisaTokenDrawSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(KaisaObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("SFD·076/221", playEvent.Payload["playedCardNo"]);
        Assert.Equal(4, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.False(playEvent.Payload.ContainsKey("targetObjectIds"));

        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaTokenDrawSpellObjectId, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("机器人", tokenEvent.Payload["tokenName"]);
        Assert.Equal(3, tokenEvent.Payload["power"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);

        var drawEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaTokenDrawSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal([KaisaTokenDrawSpellDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(KaisaTokenDrawSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaTokenDrawSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, result.State.ObjectLocations[tokenObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaTokenDrawSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaPlaysLowCostGraveyardCopyTokenSpellWhenExactlyOneUnitTargetIsLegal()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardCopyTokenSpellState(extraUnitTarget: false),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-copy-token-spell", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Complete(result, KaisaCopyTokenSpellObjectId, [KaisaObjectId]);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, conquestTrigger.Payload["effectId"]);
        Assert.Equal(KaisaCopyTokenSpellObjectId, conquestTrigger.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedObjectId"] as string, KaisaCopyTokenSpellObjectId, StringComparison.Ordinal));
        Assert.Equal(KaisaObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("UNL-200/219", playEvent.Payload["playedCardNo"]);
        Assert.Equal(3, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Stack, playEvent.Payload["destinationZone"]);
        Assert.Equal([KaisaObjectId], Assert.IsType<string[]>(playEvent.Payload["targetObjectIds"]));

        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaCopyTokenSpellObjectId, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("映像", tokenEvent.Payload["tokenName"]);
        Assert.Equal(0, tokenEvent.Payload["power"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        Assert.Equal(KaisaObjectId, tokenEvent.Payload["copiedTargetObjectId"]);
        Assert.Equal("OGN·112/298", tokenEvent.Payload["copiedCardNo"]);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, tokenEvent.Payload["tokenCardNo"]);
        var tokenTags = Assert.IsType<string[]>(tokenEvent.Payload["tokenTags"]);
        Assert.DoesNotContain(CardObjectTags.Ephemeral, tokenTags);
        Assert.DoesNotContain("映像", tokenTags);

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal([KaisaCopyTokenSpellObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle, recycleEvent.Payload["reason"]);

        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal("OGN·112/298", result.State.CardObjects[tokenObjectId].CardNo);
        Assert.Equal(6, result.State.CardObjects[tokenObjectId].Power);
        Assert.Contains(CardObjectTags.Ephemeral, result.State.CardObjects[tokenObjectId].Tags);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, result.State.CardObjects[tokenObjectId].TokenFactoryCardNo);
        Assert.DoesNotContain(KaisaCopyTokenSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([KaisaCopyTokenSpellObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerZones.Base, result.State.ObjectLocations[tokenObjectId].Zone);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[KaisaCopyTokenSpellObjectId].Zone);
    }

    [Fact]
    public async Task KaisaDoesNotAutoSelectCopyTokenGraveyardSpellWhenMultipleUnitTargetsAreLegal()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestGraveyardCopyTokenSpellState(extraUnitTarget: true),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-graveyard-copy-token-spell-multiple-targets", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await RecastTestDriver.Open(result);
        Assert.NotNull(result.State.PendingEffectPlay);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED_FROM_GRAVEYARD", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaCopyTokenSpellObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));

        Assert.Contains(KaisaObjectId, result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(KaisaExtraCopyTokenTargetObjectId, result.State.PlayerZones["P2"].Base);
        Assert.Contains(KaisaCopyTokenSpellObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations[KaisaObjectId].Zone);
        Assert.Equal(TriggerZones.Base, result.State.ObjectLocations[KaisaExtraCopyTokenTargetObjectId].Zone);
        Assert.Equal(TriggerZones.Graveyard, result.State.ObjectLocations[KaisaCopyTokenSpellObjectId].Zone);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(2, 2)]
    public async Task RumbleCombatConquestUsesChosenRecycleAndNormalCost(int power, int mana)
    {
        var result = await OfficialGraveyardRecastTests.Act(BuildNaturalConquestRumbleState(power, mana), "P1",
            new DeclareBattleCommand(BattlefieldId, [RumbleObjectId], [DefenderObjectId],["COMBAT_ASSIGNMENT"]));
        result = await OpenRumbleChoice(result);
        var choice = result.State.PendingCardChoice!;
        var paid = await OfficialGraveyardRecastTests.Act(result.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[RumbleRecycledUnitObjectId]));
        Assert.Equal(power,paid.State.PendingEffectPlay!.ManaReduction);
        var done = await OfficialGraveyardRecastTests.Act(paid.State,"P1",new PlayCardCommand(RumbleGraveyardMechanicalUnitObjectId,"SFD·075/221",[]));
        Assert.Contains(RumbleRecycledUnitObjectId,done.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(RumbleGraveyardMechanicalUnitObjectId,done.State.PlayerZones["P1"].Base);
        Assert.Equal(0,done.State.RunePools["P1"].Mana); Assert.Equal(0,done.State.RunePools["P1"].Power);
        Assert.True(done.State.CardObjects[RumbleGraveyardMechanicalUnitObjectId].IsExhausted);
        OfficialGraveyardRecastTests.Restore(done.State);
    }

    [Fact]
    public async Task RumbleDeclinesBeforePayingRecycleCost()
    {
        var result=await OfficialGraveyardRecastTests.Act(BuildNaturalConquestRumbleState(),"P1",new DeclareBattleCommand(BattlefieldId,[RumbleObjectId],[DefenderObjectId],["COMBAT_ASSIGNMENT"]));
        result=await OpenRumbleChoice(result);var choice=result.State.PendingCardChoice!;
        var done=await OfficialGraveyardRecastTests.Act(result.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Contains(RumbleRecycledUnitObjectId,done.State.PlayerZones["P1"].Base);
        Assert.Contains(RumbleGraveyardMechanicalUnitObjectId,done.State.PlayerZones["P1"].Graveyard);
        Assert.Null(done.State.PendingEffectPlay);OfficialGraveyardRecastTests.Restore(done.State);
    }

    [Fact]
    public async Task RumbleInsufficientManaKeepsPaidRecycleButRejectsPlay()
    {
        var result=await OfficialGraveyardRecastTests.Act(BuildNaturalConquestRumbleState(2,1),"P1",new DeclareBattleCommand(BattlefieldId,[RumbleObjectId],[DefenderObjectId],["COMBAT_ASSIGNMENT"]));
        result=await OpenRumbleChoice(result);var choice=result.State.PendingCardChoice!;
        var paid=await OfficialGraveyardRecastTests.Act(result.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[RumbleRecycledUnitObjectId]));
        var command=new PlayCardCommand(RumbleGraveyardMechanicalUnitObjectId,"SFD·075/221",[]);
        var rejected=await new CoreRuleEngine().ResolveAsync(paid.State,new("no-mana","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(paid.State),MatchStateHasher.Hash(rejected.State));
        var pending=paid.State.PendingEffectPlay!;
        var done=await OfficialGraveyardRecastTests.Act(paid.State,"P1",new ChooseCardsCommand(pending.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Contains(RumbleRecycledUnitObjectId,done.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(RumbleGraveyardMechanicalUnitObjectId,done.State.PlayerZones["P1"].Graveyard);OfficialGraveyardRecastTests.Restore(done.State);
    }

    private static async Task<ResolutionResult> OpenRumbleChoice(ResolutionResult result)
    {
        for(var i=0;i<12 && result.State.PendingCardChoice is null;i++) result=await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Equal("RECYCLE_FOR_EFFECT_PLAY",result.State.PendingCardChoice!.ChoiceWindow);
        OfficialGraveyardRecastTests.Restore(result.State);return result;
    }

    [Fact]
    public async Task CrimsonSignetTreantRepeatsUnitConquestTriggerAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestTreantState(),
            new PlayerIntent("intent-natural-unit-conquest-treant-repeat", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [TreantObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal));

        var conquestTriggers = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["effectId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, conquestTriggers.Length);

        var boonEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["abilityId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["targetObjectId"] as string, TreantObjectId, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, boonEvents.Length);
        Assert.False(Assert.IsType<bool>(boonEvents[0].Payload["alreadyHadBoon"]));
        Assert.True(Assert.IsType<bool>(boonEvents[1].Payload["alreadyHadBoon"]));

        var treant = result.State.CardObjects[TreantObjectId];
        Assert.Equal(5, treant.Power);
        Assert.Contains(CardObjectTags.Boon, treant.Tags);
    }

    [Fact]
    public async Task YetiBrawlerCreatesTwoDormantGoldAfterOverkillNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestYetiState(),
            new PlayerIntent("intent-natural-unit-conquest-yeti-overkill-gold", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [YetiObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var conqueredEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal));
        Assert.Equal(5, Assert.IsType<int>(conqueredEvent.Payload["assignedOverkillDamageToEnemyUnits"]));

        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestOverkillCreateDormantGold, conquestTrigger.Payload["effectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var tokenEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["abilityId"] as string, TriggerKinds.UnitConquestOverkillCreateDormantGold, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, tokenEvents.Length);

        var tokenObjectIds = tokenEvents
            .Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["tokenObjectId"]))
            .ToArray();
        Assert.Equal(tokenObjectIds, tokenObjectIds.Distinct(StringComparer.Ordinal).ToArray());
        Assert.All(tokenObjectIds, tokenObjectId =>
        {
            Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
            var tokenState = result.State.CardObjects[tokenObjectId];
            Assert.True(tokenState.IsExhausted);
            Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
            Assert.Contains("金币", tokenState.Tags);
            Assert.Contains("反应", tokenState.Tags);
        });
    }

    [Fact]
    public async Task TryndamereGainsScoreAfterAttackOverkillNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestTryndamereState(),
            new PlayerIntent("intent-natural-unit-conquest-tryndamere-overkill-score", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [TryndamereObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var conqueredEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TryndamereObjectId, StringComparison.Ordinal));
        Assert.Equal(7, Assert.IsType<int>(conqueredEvent.Payload["assignedOverkillDamageToEnemyUnits"]));

        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TryndamereObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestAttackOverkillGainScore, conquestTrigger.Payload["effectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);
        Assert.Equal(7, conquestTrigger.Payload["assignedOverkillDamageToEnemyUnits"]);
        Assert.Equal(5, conquestTrigger.Payload["requiredOverkillDamage"]);

        var scoreEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, scoreEvents.Length);
        var tryndamereScore = Assert.Single(scoreEvents, gameEvent =>
            string.Equals(gameEvent.Payload["reason"] as string, TriggerKinds.UnitConquestAttackOverkillGainScore, StringComparison.Ordinal));
        Assert.Equal(1, tryndamereScore.Payload["amount"]);
        Assert.Equal(8, tryndamereScore.Payload["score"]);
        Assert.Equal(TryndamereObjectId, tryndamereScore.Payload["sourceObjectId"]);

        Assert.Equal(8, result.State.PlayerScores["P1"]);
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["winnerPlayerId"] as string, "P1", StringComparison.Ordinal));
    }

    private static MatchState BuildNaturalConquestState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 4, "OGN·039/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [DrawObjectId] = Unit(DrawObjectId, "P1", 2)
        };

        return new MatchState(
            "natural-unit-conquest-trigger-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    MainDeck = [DrawObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
                [DrawObjectId] = new("P1", "MAIN_DECK")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestGraveyardSpellState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 6, "OGN·112/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [KaisaSpellObjectId] = Spell(KaisaSpellObjectId, "P1", "OGN·048/298", 2),
            [KaisaSpellDrawObjectId] = Unit(KaisaSpellDrawObjectId, "P1", 2)
        };

        return new MatchState(
            "natural-unit-conquest-kaisa-graveyard-spell-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    MainDeck = [KaisaSpellDrawObjectId],
                    Graveyard = [KaisaSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 3,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
                [KaisaSpellObjectId] = new("P1", "GRAVEYARD"),
                [KaisaSpellDrawObjectId] = new("P1", "MAIN_DECK")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestGraveyardRuneSpellState(bool runeDeckAvailable)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 6, "OGN·112/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [KaisaRuneSpellObjectId] = Spell(KaisaRuneSpellObjectId, "P1", "OGN·134/298", 2)
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
            [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
            [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
            [KaisaRuneSpellObjectId] = new("P1", "GRAVEYARD")
        };
        if (runeDeckAvailable)
        {
            cardObjects[KaisaCalledRuneObjectId] = Rune(KaisaCalledRuneObjectId, "P1");
            objectLocations[KaisaCalledRuneObjectId] = new("P1", "RUNE_DECK");
        }
        else
        {
            cardObjects[KaisaRuneFallbackDrawObjectId] = Unit(KaisaRuneFallbackDrawObjectId, "P1", 2);
            objectLocations[KaisaRuneFallbackDrawObjectId] = new("P1", "MAIN_DECK");
        }

        return new MatchState(
            "natural-unit-conquest-kaisa-graveyard-rune-spell-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    MainDeck = runeDeckAvailable ? [] : [KaisaRuneFallbackDrawObjectId],
                    Graveyard = [KaisaRuneSpellObjectId],
                    RuneDeck = runeDeckAvailable ? [KaisaCalledRuneObjectId] : []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 3,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations,
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestGraveyardTokenSpellState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 6, "OGN·112/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [KaisaTokenSpellObjectId] = Spell(KaisaTokenSpellObjectId, "P1", "OGN·094/298", 3)
        };

        return new MatchState(
            "natural-unit-conquest-kaisa-graveyard-token-spell-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    Graveyard = [KaisaTokenSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 4,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
                [KaisaTokenSpellObjectId] = new("P1", "GRAVEYARD")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestGraveyardTokenDrawSpellState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 6, "OGN·112/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [KaisaTokenDrawSpellObjectId] = Spell(KaisaTokenDrawSpellObjectId, "P1", "SFD·076/221", 4),
            [KaisaTokenDrawSpellDrawObjectId] = Unit(KaisaTokenDrawSpellDrawObjectId, "P1", 2)
        };

        return new MatchState(
            "natural-unit-conquest-kaisa-graveyard-token-draw-spell-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    MainDeck = [KaisaTokenDrawSpellDrawObjectId],
                    Graveyard = [KaisaTokenDrawSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(0, 2), ["P2"] = RunePool.Empty },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 5,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
                [KaisaTokenDrawSpellObjectId] = new("P1", "GRAVEYARD"),
                [KaisaTokenDrawSpellDrawObjectId] = new("P1", "MAIN_DECK")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestGraveyardCopyTokenSpellState(bool extraUnitTarget)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 6, "OGN·112/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [KaisaCopyTokenSpellObjectId] = Spell(KaisaCopyTokenSpellObjectId, "P1", "UNL-200/219", 3)
        };
        var p2Base = Array.Empty<string>();
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
            [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
            [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
            [KaisaCopyTokenSpellObjectId] = new("P1", "GRAVEYARD")
        };
        if (extraUnitTarget)
        {
            cardObjects[KaisaExtraCopyTokenTargetObjectId] = Unit(
                KaisaExtraCopyTokenTargetObjectId,
                "P2",
                4,
                "SFD·068/221") with
            {
                Tags = [CardObjectTags.UnitCard, "机械"]
            };
            p2Base = [KaisaExtraCopyTokenTargetObjectId];
            objectLocations[KaisaExtraCopyTokenTargetObjectId] = new("P2", TriggerZones.Base);
        }

        return new MatchState(
            "natural-unit-conquest-kaisa-graveyard-copy-token-spell-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    Graveyard = [KaisaCopyTokenSpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base,
                    Battlefields = [DefenderObjectId]
                }
            },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(0, 2), ["P2"] = RunePool.Empty },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 4,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations,
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestRumbleState(
        int recycledUnitPower = 4,
        int p1Mana = 0,
        int graveyardMechanicalManaCost = 4)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [RumbleObjectId] = Unit(RumbleObjectId, "P1", 4, "SFD·026/221") with
            {
                Tags = [CardObjectTags.UnitCard, "机械", "约德尔人"]
            },
            [RumbleRecycledUnitObjectId] = Unit(RumbleRecycledUnitObjectId, "P1", recycledUnitPower, "SFD·125/221"),
            [RumbleGraveyardMechanicalUnitObjectId] = Unit(RumbleGraveyardMechanicalUnitObjectId, "P1", 2, "SFD·075/221") with
            {
                ManaCost = graveyardMechanicalManaCost,
                IsExhausted = true,
                Tags = [CardObjectTags.UnitCard, "机械"]
            },
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-rumble-graveyard-mechanical-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(p1Mana, 1),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, RumbleObjectId],
                    Base = [RumbleRecycledUnitObjectId],
                    Graveyard = [RumbleGraveyardMechanicalUnitObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [RumbleObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [RumbleRecycledUnitObjectId] = new("P1", "BASE"),
                [RumbleGraveyardMechanicalUnitObjectId] = new("P1", "GRAVEYARD"),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestTreantState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [TreantObjectId] = Unit(TreantObjectId, "P1", 4, "UNL-029/219"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-trigger-treant-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, TreantObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [TreantObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestYetiState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [YetiObjectId] = Unit(YetiObjectId, "P1", 6, "UNL-018/219"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-yeti-overkill-trigger-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, YetiObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [YetiObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestTryndamereState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [TryndamereObjectId] = Unit(TryndamereObjectId, "P1", 8, "OGN·034/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-tryndamere-overkill-score-trigger-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldId, TryndamereObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 6,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [TryndamereObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo = "SFD·125/221")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
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

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
