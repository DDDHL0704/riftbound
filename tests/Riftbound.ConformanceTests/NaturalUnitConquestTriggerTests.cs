using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NaturalUnitConquestTriggerTests
{
    private const string BattlefieldId = "P1-NATURAL-UNIT-CONQUEST-BATTLEFIELD";
    private const string KaisaObjectId = "P1-NATURAL-CONQUEST-KAISA";
    private const string KaisaSpellObjectId = "P1-NATURAL-CONQUEST-KAISA-SPELL";
    private const string KaisaSpellDrawObjectId = "P1-NATURAL-CONQUEST-KAISA-SPELL-DRAW";
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
    public async Task RumbleRecyclesFriendlyUnitAndPlaysGraveyardMechanicalUnitAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestRumbleState(),
            new PlayerIntent("intent-natural-unit-conquest-rumble-graveyard-mechanical", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [RumbleObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, RumbleObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit, conquestTrigger.Payload["effectId"]);
        Assert.Equal(RumbleRecycledUnitObjectId, conquestTrigger.Payload["recycledObjectId"]);
        Assert.Equal(RumbleGraveyardMechanicalUnitObjectId, conquestTrigger.Payload["playedObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, RumbleObjectId, StringComparison.Ordinal));
        Assert.Equal([RumbleRecycledUnitObjectId], Assert.IsType<string[]>(recycleEvent.Payload["cardIds"]));
        Assert.Equal(TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit, recycleEvent.Payload["reason"]);
        Assert.Equal(TriggerZones.Field, recycleEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.MainDeck, recycleEvent.Payload["destinationZone"]);

        var playEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, RumbleGraveyardMechanicalUnitObjectId, StringComparison.Ordinal));
        Assert.Equal(RumbleObjectId, playEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1", playEvent.Payload["ownerPlayerId"]);
        Assert.Equal(TriggerZones.Graveyard, playEvent.Payload["sourceZone"]);
        Assert.Equal(TriggerZones.Base, playEvent.Payload["destinationZone"]);
        Assert.Equal(4, playEvent.Payload["playedCardManaCost"]);
        Assert.Equal(4, playEvent.Payload["manaCostReduction"]);
        Assert.Equal(0, playEvent.Payload["reducedManaCost"]);
        Assert.Equal(0, playEvent.Payload["paidManaCost"]);

        Assert.DoesNotContain(RumbleRecycledUnitObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(RumbleRecycledUnitObjectId, result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(RumbleGraveyardMechanicalUnitObjectId, result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(RumbleGraveyardMechanicalUnitObjectId, result.State.PlayerZones["P1"].Graveyard);

        var mechanicalUnit = result.State.CardObjects[RumbleGraveyardMechanicalUnitObjectId];
        Assert.Equal("SFD·065/221", mechanicalUnit.CardNo);
        Assert.Contains(CardObjectTags.UnitCard, mechanicalUnit.Tags);
        Assert.Contains("机械", mechanicalUnit.Tags);
        Assert.False(mechanicalUnit.IsExhausted);
        Assert.Equal(TriggerZones.MainDeck, result.State.ObjectLocations[RumbleRecycledUnitObjectId].Zone);
        Assert.Equal(TriggerZones.Base, result.State.ObjectLocations[RumbleGraveyardMechanicalUnitObjectId].Zone);
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

    private static MatchState BuildNaturalConquestRumbleState()
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
            [RumbleRecycledUnitObjectId] = Unit(RumbleRecycledUnitObjectId, "P1", 4),
            [RumbleGraveyardMechanicalUnitObjectId] = Unit(RumbleGraveyardMechanicalUnitObjectId, "P1", 2, "SFD·065/221") with
            {
                ManaCost = 4,
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
