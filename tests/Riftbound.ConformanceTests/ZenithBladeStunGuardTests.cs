using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZenithBladeStunGuardTests
{
    [Fact]
    public async Task ZenithBladeStunsEnemyBattlefieldUnit()
    {
        var engine = new CoreRuleEngine();
        var state = BuildZenithBladeState();

        var played = await PlayZenithBladeAsync(engine, state, "P2-ZENITH-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-zenith-blade-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-zenith-blade-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-ZENITH-BLADE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-ZENITH-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects["P2-ZENITH-BATTLEFIELD-UNIT"].UntilEndOfTurnEffects);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-ZENITH-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectId"] as string, "STUNNED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-HAND-UNIT")]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    public async Task ZenithBladeRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildZenithBladeState();

        var result = await PlayZenithBladeAsync(new CoreRuleEngine(), state, targetObjectId);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public void ZenithBladePromptChoicesOnlyExposeEnemyPublicBattlefieldUnits()
    {
        var state = BuildZenithBladeState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-ZENITH-BLADE", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Contains("P2-ZENITH-BATTLEFIELD-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BASE-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-HAND-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-BATTLEFIELD-UNIT", firstTargetChoiceIds);
    }

    private static async Task<ResolutionResult> PlayZenithBladeAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-zenith-blade-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-ZENITH-BLADE",
                "OGN·262/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ZENITH-BLADE", "P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal));
    }

    private static MatchState BuildZenithBladeState()
    {
        return new MatchState(
            roomId: "zenith-blade-target-guard-test",
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
                    Hand = ["P1-SPELL-ZENITH-BLADE", "P1-HAND-UNIT"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-ZENITH-BATTLEFIELD-UNIT",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-ZENITH-BLADE"] = ZenithBlade(),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT"),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = Unit("P1-FRIENDLY-BATTLEFIELD-UNIT"),
                ["P2-HAND-UNIT"] = Unit("P2-HAND-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-ZENITH-BATTLEFIELD-UNIT"] = Unit("P2-ZENITH-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState ZenithBlade()
    {
        return new CardObjectState(
            "P1-SPELL-ZENITH-BLADE",
            cardNo: "OGN·262/298",
            manaCost: 3,
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
