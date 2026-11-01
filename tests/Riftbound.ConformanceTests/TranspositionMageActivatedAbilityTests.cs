using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TranspositionMageActivatedAbilityTests
{
    private const string CardNo = "RAD·115/167";
    private const string AbilityId = P4ActivatedAbilityCatalog.TranspositionMageAbilityId;

    private static MatchState Position(bool eligibleHandUnit = true, bool targetOwnedByOpponent = false,
        int targetPower = 5)
    {
        var targetOwnerId = targetOwnedByOpponent ? "P2" : "P1";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF"] = new("BF", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["MAGE"] = new("MAGE", manaCost: 6, power: 4, cardNo: CardNo, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard]),
            ["TARGET"] = new("TARGET", manaCost: 5, power: targetPower, cardNo: "OGN·001/298", ownerId: targetOwnerId, controllerId: "P1",
                tags: [CardObjectTags.UnitCard], untilEndOfTurnPowerModifier: targetPower - 5),
            ["PLAY"] = new("PLAY", manaCost: 6, power: 4, cardNo: eligibleHandUnit ? CardNo : "TEST·SPELL",
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

    [Fact]
    public async Task ReturnsTargetAndOffersOnlyLowerPowerHandUnitsForFreePlay()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        var pending = game.State.PendingEffectPlay;
        Assert.NotNull(pending);
        Assert.Equal("P1", pending.PlayerId);
        Assert.Equal(4, pending.MaximumUnitPower);
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

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    public async Task EqualOrHigherPowerHandUnitIsExcludedEvenAtZero(int targetPower, int handPower)
    {
        var initial = Position(targetPower: targetPower);
        var handCardNo = handPower switch { 0 => "UNL-053/219", 5 => "OGN·001/298", _ => "OGN·075/298" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["PLAY"] = OfficialGarenConquestBattlefieldCountTests.Printed("PLAY", handCardNo, "P1") } };
        Assert.Equal(handPower, initial.CardObjects["PLAY"].Power);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();

        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(item.Event.Payload["reason"], "NO_ELIGIBLE_HAND_UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AReturnedPrintedUnitCanOnlyReplayItselfIfItsPowerActuallyDecreased(bool buffed)
    {
        var initial = Position(eligibleHandUnit: false);
        var target = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", CardNo, "P1");
        if (buffed) target = target with { Power = target.Power + 1, UntilEndOfTurnPowerModifier = 1 };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = target } };
        var originalPower = target.Power;
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        if (buffed)
        {
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal(originalPower - 1, pending.MaximumUnitPower);
            Assert.Equal(["TARGET"], pending.Sources.Keys.ToArray());
            await game.Act("P1", new PlayCardCommand("TARGET", CardNo, [], Destination: "BASE"));
            Assert.Contains("TARGET", game.State.PlayerZones["P1"].Base);
            Assert.Equal(originalPower - 1, game.State.CardObjects["TARGET"].Power);
        }
        else Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturningATokenStillAllowsTheFollowingOptionalPlay(bool decline)
    {
        var initial = Position();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T01", out var robot));
        var target = robot.CreateObject("TARGET", "P1", "P1") with { Power = 5, UntilEndOfTurnPowerModifier = 2 };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = target } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("TARGET"));
        Assert.DoesNotContain("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "TOKEN_CEASED_TO_EXIST");
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(4, pending.MaximumUnitPower);
        Assert.Equal(["PLAY"], pending.Sources.Keys.ToArray());
        if (decline)
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        else
            await game.Act("P1", new PlayCardCommand("PLAY", CardNo, [], Destination: "BASE"));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(!decline, game.State.PlayerZones["P1"].Base.Contains("PLAY"));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ACopiedMageCanFinishItsSkillAfterCeasingToExist(bool returnsSelf, bool decline)
    {
        var initial = Position();
        var mage = OfficialGarenConquestBattlefieldCountTests.Printed("MAGE", CardNo, "P1") with {
            TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
            CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, CardNo)],
            Power = returnsSelf ? 5 : 4, UntilEndOfTurnPowerModifier = returnsSelf ? 1 : 0 };
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["MAGE"] = mage,
                ["RETURN"] = OfficialGarenConquestBattlefieldCountTests.Printed("RETURN", "OGN·104/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = ["PLAY", "RETURN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["RETURN"] = new("P1", "HAND") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, [returnsSelf ? "MAGE" : "TARGET"]));
        if (!returnsSelf)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["MAGE"]));
            await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey("MAGE"));
            Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation!.CeasedSource);
        }
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("MAGE"));
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        var activation = Assert.IsType<SimpleActivationContext>(pending.Parent.SimpleActivation);
        var ceased = Assert.IsType<CeasedSimpleActivationSource>(activation.CeasedSource);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, ceased.TokenFactoryCardNo);
        Assert.Equal(["PLAY"], pending.Sources.Keys.ToArray());
        foreach (var invalid in new CeasedSimpleActivationSource?[] {
            null, ceased with { DepartedTick = activation.AcquiredTick - 1 },
            ceased with { DepartedTick = game.State.Tick + 1 }, ceased with { TokenFactoryCardNo = "UNL·T05" } })
        {
            var forged = pending with { Parent = pending.Parent with {
                SimpleActivation = activation with { CeasedSource = invalid } } };
            Assert.Contains("invalid revealed hand effect-play continuation",
                MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with { PendingEffectPlay = forged }));
        }
        if (decline)
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        else
            await game.Act("P1", new PlayCardCommand("PLAY", CardNo, [], Destination: "BASE"));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(!decline, game.State.PlayerZones["P1"].Base.Contains("PLAY"));
        await game.Replay();
    }

    [Fact]
    public async Task ACompletedParentCannotBeInventedAsAPersistedActivation()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        var forged = game.State with { PendingEffectPlay = null,
            StackItems = [pending.Parent with { EffectPlayCompleted = true }] };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
    }

    [Theory]
    [InlineData("ANY_BATTLEFIELD")]
    [InlineData("BASE")]
    [InlineData("original-location")]
    [InlineData("original-battlefield")]
    [InlineData("foreign-source")]
    [InlineData("viewed-cards")]
    public async Task RecoveryRejectsChangingTheHandPlayPolicy(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]));
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        var forged = pending with { DestinationPolicy = mutation };
        if (mutation == "original-location") forged = pending with { OriginalLocation = new("P1", "BASE") };
        if (mutation == "original-battlefield") forged = pending with { OriginalBattlefield = new("BF", 0) };
        if (mutation == "foreign-source") forged = pending with { SourcePlayerId = "P2" };
        if (mutation == "viewed-cards") forged = pending with { ViewedCardIds = ["PLAY"] };
        Assert.Contains("invalid revealed hand effect-play continuation",
            MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with { PendingEffectPlay = forged }));
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
