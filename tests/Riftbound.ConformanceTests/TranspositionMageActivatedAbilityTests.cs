using System.Text.Json;
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
        var game = new ResponseContinuation(changed);
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

    private static async Task<MatchState> CapturedBeforeResponse()
    {
        var initial = Position();
        // Keep the offered hand unit strictly below the returned unit's power.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = initial.CardObjects["TARGET"] with { Power = 5 } } };
        var activated = await new CoreRuleEngine().ResolveAsync(initial,
            new PlayerIntent("captured-response", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("MAGE", AbilityId, ["TARGET"]), default);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        return activated.State;
    }

    private static async Task PlayOfferedHandUnit(ResponseContinuation game)
    {
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId);
        Assert.Equal(["PLAY"], pending.Sources.Keys.ToArray());
        await game.Act("P1", new PlayCardCommand("PLAY", CardNo, [], Destination: "BASE"));
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task SourceChangingControllerKeepsTheOriginalPlayersReturnAndFreePlay()
    {
        var captured = await CapturedBeforeResponse();
        // Isolate a same-object control-change response from the control spell.
        var changed = LocalDestructionRecallTests.At(captured, "MAGE", "P2", "BASE");
        Assert.Equal(captured.CardObjects["MAGE"].ObjectGeneration, changed.CardObjects["MAGE"].ObjectGeneration);
        Assert.Equal("P1", Assert.Single(changed.StackItems).ControllerId);
        var game = new ResponseContinuation(changed);
        await game.Top();
        await PlayOfferedHandUnit(game);
        Assert.Equal("P2", game.State.CardObjects["MAGE"].ControllerId);
        Assert.Contains("MAGE", game.State.PlayerZones["P2"].Base);
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetControlIsRecheckedAtResolutionAndMayBecomeFriendlyAgain(bool returnsToOriginalController)
    {
        var captured = await CapturedBeforeResponse();
        var changed = LocalDestructionRecallTests.At(captured, "TARGET", "P2", "BASE");
        if (returnsToOriginalController)
            changed = LocalDestructionRecallTests.At(changed, "TARGET", "P1", "BASE");
        Assert.Equal(captured.CardObjects["TARGET"].ObjectGeneration, changed.CardObjects["TARGET"].ObjectGeneration);
        var game = new ResponseContinuation(changed);
        await game.Top();
        if (returnsToOriginalController)
            await PlayOfferedHandUnit(game);
        else
        {
            Assert.Contains("TARGET", game.State.PlayerZones["P2"].Base);
            Assert.Contains("PLAY", game.State.PlayerZones["P1"].Hand);
            Assert.Null(game.State.PendingEffectPlay);
            Assert.Empty(game.State.StackItems);
            Assert.DoesNotContain(game.Events(), item => item.Event.Kind == "UNIT_RETURNED_TO_HAND");
            Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
                && Equals(item.Event.Payload["reason"], "TARGET_NO_LONGER_LEGAL"));
            await game.Replay();
        }
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task TargetLeavingAndReenteringTheFieldCannotReuseItsOldBinding()
    {
        var captured = await CapturedBeforeResponse();
        var target = captured.CardObjects["TARGET"];
        // Final snapshot of field -> hand -> field: the card ID and location
        // match, but the two nonfield crossings have advanced its identity.
        var reentered = captured with { CardObjects = new Dictionary<string, CardObjectState>(captured.CardObjects) {
            ["TARGET"] = target with { ObjectGeneration = target.ObjectGeneration + 2 } } };
        var game = new ResponseContinuation(reentered);
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Base);
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), item => item.Event.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(item.Event.Payload["reason"], "TARGET_NO_LONGER_LEGAL"));
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetMovingWithinTheFieldKeepsItsBinding(bool returnsToBase)
    {
        var captured = await CapturedBeforeResponse();
        var moved = LocalDestructionRecallTests.At(captured, "TARGET", "P1", "BATTLEFIELD", "BF");
        if (returnsToBase)
            moved = LocalDestructionRecallTests.At(moved, "TARGET", "P1", "BASE");
        Assert.Equal(captured.CardObjects["TARGET"].ObjectGeneration, moved.CardObjects["TARGET"].ObjectGeneration);
        var game = new ResponseContinuation(moved);
        await game.Top();
        await PlayOfferedHandUnit(game);
    }

    // These reconstructed response snapshots start after tick zero. Validate
    // component round trips and replay the continuation from that boundary;
    // the ordinary Driver below also validates the complete opening journal.
    private sealed class ResponseContinuation : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        private readonly MatchSession session;
        internal MatchState State { get; private set; }

        internal ResponseContinuation(MatchState state)
        {
            initial = OfficialInsightAndSpellLockTests.Restore(state);
            State = initial;
            session = new(initial, new CoreRuleEngine(), this);
        }

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            entries.Add(entry);
            return ValueTask.CompletedTask;
        }

        private RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(
            e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick,
            e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();

        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((item, index) =>
            new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, item))).ToArray();

        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json);
            command = GameCommandJsonMapper.Map(wire);
            var intent = "response-" + entries.Count;
            var result = await session.SubmitAsync(player, intent, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            OfficialInsightAndSpellLockTests.Restore(State);
            var count = entries.Count;
            var duplicate = await session.SubmitAsync(player, intent, command, wire, default);
            Assert.True(duplicate.Accepted);
            Assert.Equal(count, entries.Count);
            Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(duplicate.State));
            await Replay();
        }

        internal async Task Top()
        {
            var top = State.StackItems[^1].StackItemId;
            for (var passes = 0; State.StackItems.Any(item => item.StackItemId == top)
                && State.PriorityPlayerId is { } player; passes++)
            {
                Assert.True(passes < 4, "The captured skill did not resolve after both players passed.");
                await Act(player, new PassPriorityCommand());
            }
        }

        internal async Task Replay()
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State,
                new CoreRuleEngine(), default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
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
        Assert.Equal(activation.SourceFaceRevision, ceased.SourceFaceRevision);
        Assert.Equal(["PLAY"], pending.Sources.Keys.ToArray());
        foreach (var invalid in new CeasedSimpleActivationSource?[] {
            null, ceased with { DepartedTick = activation.AcquiredTick - 1 },
            ceased with { DepartedTick = game.State.Tick + 1 }, ceased with { TokenFactoryCardNo = "UNL·T05" },
            ceased with { SourceFaceRevision = activation.SourceFaceRevision + 1 } })
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
