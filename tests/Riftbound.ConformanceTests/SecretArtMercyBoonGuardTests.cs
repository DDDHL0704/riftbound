using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SecretArtMercyBoonGuardTests
{
    [Fact]
    public async Task SecretArtMercyGrantsBoonToFriendlyPublicUnitWithoutFriendlySpellshieldTax()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSecretArtMercyState();

        var played = await PlaySecretArtMercyAsync(engine, state, "P1-FRIENDLY-SPELLSHIELD-UNIT");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);

        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal(3, Assert.IsType<int>(costEvent.Payload["mana"]));
        Assert.Equal(3, Assert.IsType<int>(costEvent.Payload["baseMana"]));
        Assert.Equal(0, Assert.IsType<int>(costEvent.Payload["spellshieldTaxMana"]));
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));

        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-secret-art-mercy-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-secret-art-mercy-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(3, p2Pass.State.Tick);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SECRET-ART-MERCY"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-FRIENDLY-SPELLSHIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);

        var target = p2Pass.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Equal(0, target.UntilEndOfTurnPowerModifier);
        Assert.Contains(CardObjectTags.Boon, target.Tags);
        Assert.Contains(CardObjectTags.UnitCard, target.Tags);
        Assert.Contains("法盾", target.Tags);
        Assert.Equal(3, target.Tags.Count);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-SPELLSHIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tag"] as string, CardObjectTags.Boon, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-SPELLSHIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == 1
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 3);
    }

    [Theory]
    [InlineData("P2-ENEMY-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-EQUIPMENT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-SPELL", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-RUNE", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-STALE-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FACE-DOWN-STANDBY", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-UNIT", 2, ErrorCodes.InsufficientCost)]
    public async Task SecretArtMercyRejectsInvalidTargetsWithoutMutation(
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSecretArtMercyState(mana);

        var result = await PlaySecretArtMercyAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        AssertNoMutation(result, mana);
    }

    [Theory]
    [InlineData("P1-BASE-SECRET-ART-MERCY", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-SPELL-SECRET-ART-MERCY", 3, ErrorCodes.CardNotInHand)]
    public async Task SecretArtMercyRejectsInvalidSourcesWithoutMutation(
        string sourceObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSecretArtMercyState(mana);

        var result = await PlaySecretArtMercyAsync(new CoreRuleEngine(), state, "P1-FRIENDLY-UNIT", sourceObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        AssertNoMutation(result, mana);
    }

    [Fact]
    public async Task SecretArtMercyAlreadyBoonedTargetDoesNotDuplicateBoonOrPower()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSecretArtMercyState(alreadyBoonedTarget: true);

        var played = await PlaySecretArtMercyAsync(engine, state, "P1-FRIENDLY-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-secret-art-mercy-already-boon-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-secret-art-mercy-already-boon-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var target = p2Pass.State.CardObjects["P1-FRIENDLY-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Contains(CardObjectTags.Boon, target.Tags);
        Assert.Contains(CardObjectTags.UnitCard, target.Tags);
        Assert.Equal(2, target.Tags.Count);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void SecretArtMercyPromptOffersLegacyCustomTagFriendlyUnitButNotNonUnits()
    {
        var state = WithLegacyCustomTagFriendlyUnit(BuildSecretArtMercyState());

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
            requirement => string.Equals(
                requirement["sourceObjectId"] as string,
                "P1-SPELL-SECRET-ART-MERCY",
                StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var targetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Contains("P1-FRIENDLY-UNIT", targetChoiceIds);
        Assert.Contains("P1-FRIENDLY-CUSTOM-UNIT", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-EQUIPMENT", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-SPELL", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-RUNE", targetChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", targetChoiceIds);
    }

    private static async Task<ResolutionResult> PlaySecretArtMercyAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId,
        string sourceObjectId = "P1-SPELL-SECRET-ART-MERCY")
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-secret-art-mercy-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "OGN·053/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static void AssertNoMutation(ResolutionResult result, int mana)
    {
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SECRET-ART-MERCY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SECRET-ART-MERCY"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-FRIENDLY-UNIT",
                "P1-FRIENDLY-SPELLSHIELD-UNIT",
                "P1-FRIENDLY-EQUIPMENT",
                "P1-FRIENDLY-SPELL",
                "P1-FRIENDLY-RUNE",
                "P1-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-SPELL-SECRET-ART-MERCY"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);

        Assert.Equal(2, result.State.CardObjects["P1-FRIENDLY-UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["P1-FRIENDLY-UNIT"].Tags);
        Assert.Equal(2, result.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾"], result.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"].Tags);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
    }

    private static MatchState BuildSecretArtMercyState(
        int mana = 3,
        bool alreadyBoonedTarget = false)
    {
        return new MatchState(
            roomId: "secret-art-mercy-boon-guard-test",
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
                    Hand = ["P1-SPELL-SECRET-ART-MERCY"],
                    Base = ["P1-BASE-SECRET-ART-MERCY"],
                    Battlefields =
                    [
                        "P1-FRIENDLY-UNIT",
                        "P1-FRIENDLY-SPELLSHIELD-UNIT",
                        "P1-FRIENDLY-EQUIPMENT",
                        "P1-FRIENDLY-SPELL",
                        "P1-FRIENDLY-RUNE",
                        "P1-FACE-DOWN-STANDBY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-SECRET-ART-MERCY"],
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SECRET-ART-MERCY"] = SecretArtMercy("P1-SPELL-SECRET-ART-MERCY"),
                ["P1-BASE-SECRET-ART-MERCY"] = SecretArtMercy("P1-BASE-SECRET-ART-MERCY"),
                ["P2-SPELL-SECRET-ART-MERCY"] = SecretArtMercy(
                    "P2-SPELL-SECRET-ART-MERCY",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-FRIENDLY-UNIT"] = Unit(
                    "P1-FRIENDLY-UNIT",
                    power: alreadyBoonedTarget ? 3 : 2,
                    tags: alreadyBoonedTarget ? [CardObjectTags.Boon, CardObjectTags.UnitCard] : [CardObjectTags.UnitCard]),
                ["P1-FRIENDLY-SPELLSHIELD-UNIT"] = Unit(
                    "P1-FRIENDLY-SPELLSHIELD-UNIT",
                    tags: [CardObjectTags.UnitCard, "法盾"]),
                ["P1-FRIENDLY-EQUIPMENT"] = NonUnit("P1-FRIENDLY-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-FRIENDLY-SPELL"] = NonUnit("P1-FRIENDLY-SPELL", "OGN·169/298", CardObjectTags.SpellCard),
                ["P1-FRIENDLY-RUNE"] = NonUnit("P1-FRIENDLY-RUNE", "RUNES·001", CardObjectTags.RuneCard),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-ENEMY-UNIT"] = Unit(
                    "P2-ENEMY-UNIT",
                ownerId: "P2",
                controllerId: "P2")
            });
    }

    private static MatchState WithLegacyCustomTagFriendlyUnit(MatchState state)
    {
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields =
                [
                    .. state.PlayerZones["P1"].Battlefields,
                    "P1-FRIENDLY-CUSTOM-UNIT"
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            ["P1-FRIENDLY-CUSTOM-UNIT"] = Unit(
                "P1-FRIENDLY-CUSTOM-UNIT",
                tags: ["黄沙士兵"])
        };

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static CardObjectState SecretArtMercy(
        string objectId,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·053/298",
            manaCost: 3,
            tags: [CardObjectTags.SpellCard],
            ownerId: ownerId,
            controllerId: controllerId);
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
        string tag)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: "P1",
            controllerId: "P1");
    }
}
