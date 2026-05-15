using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SwitcherooGuardTests
{
    [Fact]
    public async Task SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSwitcherooState();

        var played = await PlaySwitcherooAsync(
            engine,
            state,
            "P1-SPELL-SWITCHEROO",
            ["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Equal([], played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 2);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-switcheroo-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-switcheroo-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], p2Pass.State.PlayerZones["P1"].Graveyard);

        var firstTarget = p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"];
        var secondTarget = p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"];
        Assert.Equal(5, firstTarget.Power);
        Assert.Equal(3, firstTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(2, firstTarget.Power - firstTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(2, secondTarget.Power);
        Assert.Equal(-3, secondTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(5, secondTarget.Power - secondTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
        var powerEffects = p2Pass.State.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, powerEffects.Length);
        Assert.Contains(
            powerEffects,
            effect => string.Equals(effect.TargetObjectId, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
                && string.Equals(effect.SourceCardNo, "SFD·145/221", StringComparison.Ordinal)
                && string.Equals(effect.EffectKind, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal)
                && string.Equals(effect.SourcePath, "CoreRuleEngine.ApplyPowerModifier", StringComparison.Ordinal)
                && effect.IsLayerEngineFoundationOnly
                && effect.PowerDelta == 3
                && effect.BasePower == 2
                && effect.EffectivePower == 5);
        Assert.All(
            powerEffects,
            effect => Assert.Contains("full official LayerEngine coverage", effect.DeferredLayerEngineResiduals ?? []));
        var snapshotPowerEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
                p2Pass.Snapshots["P1"].Timing["continuousEffects"])
            .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
            .ToArray();
        var firstTargetEffectView = Assert.Single(
            snapshotPowerEffects,
            effect => string.Equals(effect["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-SPELL-SWITCHEROO", firstTargetEffectView["sourceObjectId"]);
        Assert.Equal("SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", firstTargetEffectView["effectKind"]);
        Assert.Equal("CoreRuleEngine.ApplyPowerModifier", firstTargetEffectView["sourcePath"]);
        Assert.Equal("FOUNDATION_ONLY", firstTargetEffectView["layerEngineStatus"]);
        Assert.Contains(
            "timestamp ordering",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(firstTargetEffectView["deferredLayerEngineResiduals"]));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == 3
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 5);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == -3
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 2);
    }

    [Theory]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BASE-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "UNKNOWN-TARGET", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-EQUIPMENT", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-SPELL", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-RUNE", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-FACE-DOWN-STANDBY", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.CardNotInHand)]
    [InlineData("P2-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.CardNotInHand)]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 1, ErrorCodes.InsufficientCost)]
    public async Task SwitcherooRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string firstTargetObjectId,
        string secondTargetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSwitcherooState(mana);

        var result = await PlaySwitcherooAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            [firstTargetObjectId, secondTargetObjectId]);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SWITCHEROO", "P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-SPELL-SWITCHEROO"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE",
                "P2-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        AssertPowerStateUnchanged(result.State);
        Assert.Null(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P2-FACE-DOWN-STANDBY"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SwitcherooResolutionSkipsPowerMutationWhenTargetLeavesBattlefield()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSwitcherooState();

        var played = await PlaySwitcherooAsync(
            engine,
            state,
            "P1-SPELL-SWITCHEROO",
            ["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);

        var dirtyState = played.State with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = played.State.PlayerZones["P1"],
                ["P2"] = played.State.PlayerZones["P2"] with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY"
                    ],
                    Base = ["P2-BATTLEFIELD-UNIT"]
                }
            }
        };

        var p1Pass = await engine.ResolveAsync(
            dirtyState,
            new PlayerIntent("intent-switcheroo-dirty-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-switcheroo-dirty-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySwitcherooAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-switcheroo-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·145/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildSwitcherooState(int mana = 2)
    {
        return new MatchState(
            roomId: "switcheroo-guard-test",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SWITCHEROO"],
                    Base = ["P1-BASE-SWITCHEROO", "P1-BASE-UNIT"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-SWITCHEROO"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SWITCHEROO"] = Switcheroo("P1-SPELL-SWITCHEROO"),
                ["P1-BASE-SWITCHEROO"] = Switcheroo("P1-BASE-SWITCHEROO"),
                ["P2-SPELL-SWITCHEROO"] = Switcheroo(
                    "P2-SPELL-SWITCHEROO",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT", "P1", 2, damage: 1),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1", 4),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", "P2", 5, isExhausted: true),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SPELL"] = new(
                    "P2-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNE"] = new(
                    "P2-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-DOWN-STANDBY"] = new(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static CardObjectState Switcheroo(
        string objectId,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·145/221",
            manaCost: 2,
            tags: [CardObjectTags.SpellCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        int damage = 0,
        bool isExhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            damage: damage,
            power: power,
            isExhausted: isExhausted,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static void AssertPowerStateUnchanged(MatchState state)
    {
        Assert.Equal(2, state.CardObjects["P1-BATTLEFIELD-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P1-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(1, state.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(5, state.CardObjects["P2-BATTLEFIELD-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(0, state.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.True(state.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(4, state.CardObjects["P1-BASE-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P1-BASE-UNIT"].UntilEndOfTurnPowerModifier);
    }
}
