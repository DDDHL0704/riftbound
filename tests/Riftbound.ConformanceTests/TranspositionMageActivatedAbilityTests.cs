using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TranspositionMageActivatedAbilityTests
{
    private const string CardNo = "RAD·115/167";
    private const string AbilityId = P4ActivatedAbilityCatalog.TranspositionMageAbilityId;

    private static MatchState Position(bool eligibleHandUnit = true, int handPower = 4, bool targetOwnedByOpponent = false)
    {
        var targetOwnerId = targetOwnedByOpponent ? "P2" : "P1";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF"] = new("BF", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["MAGE"] = new("MAGE", manaCost: 6, power: 4, cardNo: CardNo, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard]),
            ["TARGET"] = new("TARGET", manaCost: 2, power: 4, cardNo: "TEST·UNIT", ownerId: targetOwnerId, controllerId: "P1",
                tags: [CardObjectTags.UnitCard]),
            ["PLAY"] = new("PLAY", manaCost: 6, power: handPower, cardNo: eligibleHandUnit ? CardNo : "TEST·SPELL",
                ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard])
        };
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF"] = new("P1", "BATTLEFIELD", "BF"),
            ["MAGE"] = new("P1", "BATTLEFIELD", "BF"),
            ["TARGET"] = new("P1", "BASE"),
            ["PLAY"] = new("P1", "HAND")
        };
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with { Base = ["TARGET"], Battlefields = ["BF", "MAGE"], Hand = ["PLAY"] },
            ["P2"] = PlayerZones.Empty
        };
        return new MatchState("RAD-TRANSPOSITION", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }),
                ["P2"] = new(20, 0)
            }, seed: 77);
    }

    [Fact]
    public void CatalogRequiresOnePurpleAndFriendlyUnitTarget()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(AbilityId, out var ability));
        Assert.Equal(CardNo, ability.SourceCardNo);
        Assert.Equal(1, P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability)[RuneTrait.Purple]);
        Assert.True(ability.SimpleEffect!.ReturnsFriendlyUnitThenFreePlayUpToPower);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                Assert.Single(ResolutionResult.BuildPrompts(Position())["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility).Metadata!["sourceRequirements"]),
            item => Equals(item["abilityId"], AbilityId));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Equal(["MAGE", "TARGET"], choices["0"].Select(choice => choice.Id).Order().ToArray());
    }

    private static async Task<MatchState> CapturedBeforeSourceChangesFace(bool startsAsCopy)
    {
        var initial = Position(eligibleHandUnit: false);
        if (startsAsCopy)
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["MAGE"] = initial.CardObjects["MAGE"] with {
                    TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, CardNo)] } } };
        var activated = await new CoreRuleEngine().ResolveAsync(initial,
            new PlayerIntent("captured-face", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]), default);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var state = activated.State;
        var source = state.CardObjects["MAGE"];
        var replacement = OfficialGarenConquestBattlefieldCountTests.Printed("MAGE", "OGN·001/298", "P1") with {
            IsExhausted = true, TokenFactoryCardNo = source.TokenFactoryCardNo,
            CopyFaceTransitions = [..(source.CopyFaceTransitions ?? []),
                new(state.Tick + 1, source.ObjectGeneration, CardNo, "OGN·001/298")] };
        return state with { Tick = state.Tick + 1,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["MAGE"] = replacement } };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedSkillUsesItsSourceFaceAtActivationAfterTheSourceCopiesAnotherUnit(bool startsAsCopy)
    {
        var changed = await CapturedBeforeSourceChangesFace(startsAsCopy);
        var captured = Assert.Single(changed.StackItems).SimpleActivation!;
        Assert.Equal(startsAsCopy ? 1L : 0L, captured.SourceFaceRevision);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(changed));
        var game = new Driver(changed);
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RecoveryRejectsInventedSourceFaceRevisions(long revision)
    {
        var changed = await CapturedBeforeSourceChangesFace(startsAsCopy: true);
        var parent = Assert.Single(changed.StackItems);
        var forged = changed with { StackItems = [parent with {
            SimpleActivation = parent.SimpleActivation! with { SourceFaceRevision = revision } }] };
        Assert.Contains($"stack item {parent.StackItemId} printed activation context is invalid",
            MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
    }

    [Fact]
    public async Task ReturnsTargetAndOffersEqualPowerHandUnitForFreePlay()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        var pending = game.State.PendingEffectPlay;
        Assert.NotNull(pending);
        Assert.Equal("P1", pending.PlayerId);
        Assert.Equal(["PLAY"], pending.Sources.Keys.ToArray());
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new PlayCardCommand("PLAY", CardNo, [], Destination: "BASE"));
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        Assert.Contains(game.Events(), item => item.Event.Kind == "UNIT_RETURNED_TO_HAND");
        await game.Replay();
    }

    [Fact]
    public async Task NoEligibleHandUnitStillReturnsTargetAndDoesNotOpenPlayChoice()
    {
        var game = new Driver(Position(eligibleHandUnit: false));
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(item.Event.Payload["reason"], "NO_ELIGIBLE_HAND_UNIT"));
        await game.Replay();
    }

    [Fact]
    public async Task ControlledForeignOwnedTargetReturnsToItsOwnersHand()
    {
        var game = new Driver(Position(targetOwnedByOpponent: true));
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();

        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Equal(["PLAY"], game.State.PendingEffectPlay!.Sources.Keys.ToArray());
        await game.Act("P1", new PlayCardCommand("PLAY", CardNo, [], Destination: "BASE"));
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task HigherPowerHandUnitIsExcluded()
    {
        var game = new Driver(Position(handPower: 5));
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();

        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(item.Event.Payload["reason"], "NO_ELIGIBLE_HAND_UNIT"));
        await game.Replay();
    }

    [Fact]
    public async Task WrongTargetOrMissingPurpleLeavesStateUntouched()
    {
        foreach (var (target, pool) in new[] { ("missing", new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 })), ("TARGET", new RunePool(0, 0, new Dictionary<string, int>())) })
        {
            var initial = Position();
            var state = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };
            var before = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state,
                new PlayerIntent("invalid-mage", "P1", CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("MAGE", AbilityId, [target]), default);
            Assert.False(result.Accepted);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
            Assert.Empty(result.Events);
        }
    }

    [Fact]
    public async Task ForgedEffectPlayContinuationIsRejected()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        var pending = game.State.PendingEffectPlay;
        Assert.NotNull(pending);
        var forged = game.State with
        {
            PendingEffectPlay = pending! with
            {
                Sources = new Dictionary<string, long>(pending!.Sources, StringComparer.Ordinal)
                {
                    ["FORGED"] = 0
                }
            }
        };

        Assert.Contains("invalid revealed hand effect-play continuation",
            MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
    }

}
