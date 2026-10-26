using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Current CN204.3.a explicitly uses Bullet Time as a resolution instruction
// payment. Its quantity is chosen and paid when that instruction resolves.
public sealed class InstructionPowerPaymentTimingTests
{
    [Fact]
    public async Task BulletTimeKeepsPowerUntilItsResolutionPaymentWindow()
    {
        var initial = Position();
        var engine = new CoreRuleEngine();
        var played = await engine.ResolveAsync(initial, new("instruction-cost-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(2, played.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, played.State.CardObjects["TARGET"].Damage);
        var first = await engine.ResolveAsync(played.State,
            new("instruction-first-pass", played.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var second = await engine.ResolveAsync(first.State,
            new("instruction-resolves", first.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(second.Accepted, second.ErrorMessage);
        var payment = Assert.IsType<RuleChoiceContinuation>(second.State.PendingRuleChoice);
        Assert.Equal("P1", payment.Request.PlayerId);
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", payment.Request.Kind);
        Assert.Contains(payment.Request.Options, o => o.Id == "SPEND_POWER:0");
        Assert.Contains(payment.Request.Options, o => o.Id == "SPEND_POWER:1");
        Assert.Contains(payment.Request.Options, o => o.Id == "SPEND_POWER:2");
        Assert.Equal(2, second.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, second.State.CardObjects["TARGET"].Damage);
        Assert.Equal(Assert.Single(second.State.StackItems).StackItemId, Assert.Single(payment.Origin.StackItems).StackItemId);
    }

    [Fact]
    public async Task BulletTimeRejectsAPredeclaredPlayTimePowerPayment()
    {
        var initial = Position();
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("no-early-instruction-payment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"], OptionalCosts: ["SPEND_POWER:2"]), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void NativePlayPromptDoesNotOfferResolutionPowerAsAnAdditionalCost()
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(Position())["P1"].Candidates!, item => item.Action == CommandTypes.PlayCard);
        Assert.DoesNotContain(candidate.OptionalCosts ?? [], choice => choice.Id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ResolutionPaysExactlyTheChosenQuantityBeforeApplyingDamage(int amount)
    {
        var initial = Position();
        var driver = await Open(initial);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await Decide(driver, "SPEND_POWER:" + amount);
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(2 - amount, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(amount, driver.State.CardObjects["TARGET"].Damage);
        Assert.Contains("CARD", driver.State.PlayerZones["P1"].Graveyard);
        var receipt = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
        Assert.Equal(amount, receipt.Event.Payload["totalPowerCost"]);
        Assert.Equal(["SPEND_POWER:" + amount], Assert.IsType<string[]>(receipt.Event.Payload["paymentChoiceIds"]));
        Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "PLAY_CARD"));
        var events = driver.Events();
        Assert.True(Array.IndexOf(events, receipt) < Array.FindIndex(events, e => e.Event.Kind == "DAMAGE_APPLIED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TypedPowerSelectionPreservesOtherTraitsAndWildcardPower()
    {
        var initial = WithPool(Position(), new(1, 1, new Dictionary<string, int> { ["blue"] = 1, ["red"] = 2 }));
        var driver = await Open(initial);
        await Decide(driver, "SPEND_POWER:red:2");
        Assert.Equal(1, driver.State.RunePools["P1"].Power);
        Assert.Equal(1, driver.State.RunePools["P1"].PowerByTrait["blue"]);
        Assert.False(driver.State.RunePools["P1"].PowerByTrait.ContainsKey("red"));
        Assert.Equal(2, driver.State.CardObjects["TARGET"].Damage);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuneResourceCanBeAcquiredDuringResolutionAndEitherSpentOrRetained(bool spend)
    {
        var initial = AddPermanent(WithPool(Position(), new(1, 0)), "RUNE", "OGN·089/298");
        var driver = await Open(initial);
        Assert.DoesNotContain(driver.State.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:1");
        await Decide(driver, "RECYCLE_RUNE:RUNE");
        var request = driver.State.PendingRuleChoice!.Request;
        Assert.Contains(request.Options, o => o.Id == "SPEND_POWER:1");
        Assert.Equal(1, request.Preview!.RunePools["P1"].TotalPower);
        Assert.Equal(0, request.Preview.CardObjects["TARGET"].Damage);
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        var paymentStart = driver.Events().Length;
        await Decide(driver, spend ? "SPEND_POWER:1" : "SPEND_POWER:0");
        Assert.DoesNotContain(driver.Events().Skip(paymentStart), e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.Equal(spend ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(spend ? 1 : 0, driver.State.CardObjects["TARGET"].Damage);
        Assert.Contains("RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.DragonSoulSageCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo)]
    public async Task ReactionResourceSkillsResolveBeforeTheInstructionPayment(string card)
    {
        var initial = AddPermanent(WithPool(Position(), new(1, 0)), "RESOURCE", card);
        var driver = await Open(initial);
        var option = Assert.Single(driver.State.PendingRuleChoice!.Request.Options,
            o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal));
        await Decide(driver, option.Id);
        var amount = card == P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? 0 : 1;
        var visiblePool = driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"];
        Assert.Equal(card == P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? 1 : 0, visiblePool.Mana);
        Assert.True(visiblePool.TotalPower >= amount);
        Assert.Equal(card == P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? 1 : 0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(amount, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        var paymentStart = driver.Events().Length;
        await Decide(driver, "SPEND_POWER:" + amount);
        Assert.DoesNotContain(driver.Events().Skip(paymentStart), e => e.Event.Kind == "ABILITY_ACTIVATED");
        Assert.Equal(amount, driver.State.CardObjects["TARGET"].Damage);
        Assert.Single(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task GrantedEchoAsksForIndependentQuantitiesUsingTheRemainingPool()
    {
        var initial = WithPool(Position(), new(2, 3));
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant([], "P1") };
        var driver = await Open(initial, ["ECHO"]);
        await Decide(driver, "SPEND_POWER:1");
        var second = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal(2, second.Request.Preview!.RunePools["P1"].TotalPower);
        Assert.Equal(1, second.Request.Preview.CardObjects["TARGET"].Damage);
        Assert.DoesNotContain(second.Request.Options, o => o.Id == "SPEND_POWER:3");
        await Decide(driver, "SPEND_POWER:2");
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(3, driver.State.CardObjects["TARGET"].Damage);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT")));
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Equal(1, driver.State.PlayerCardsPlayedThisTurn["P1"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PowerAcquiredAfterTheDeclarationIsAvailableToTheResolvingInstruction()
    {
        var initial = AddPermanent(WithPool(Position(), new(1, 0)), "RUNE", "OGN·089/298");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
        Assert.Equal("P1", driver.State.PriorityPlayerId);
        await driver.Act("P1", new RecycleRuneCommand("RUNE"));
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains(driver.State.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:1");
        await Decide(driver, "SPEND_POWER:1");
        Assert.Equal(1, driver.State.CardObjects["TARGET"].Damage);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ACounteredSpellNeverRequestsOrChargesItsResolutionInstruction()
    {
        var initial = Position();
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(10, 10) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Hand = ["COUNTER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["COUNTER"] = OfficialGarenConquestBattlefieldCountTests.Printed("COUNTER", "OGN·064/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["COUNTER"] = new("P2", "HAND") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
        var spell = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId]));
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(driver.State.StackItems);
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(2, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, driver.State.CardObjects["TARGET"].Damage);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingFiniteQaPositionColdRestoresItsInstructionResourceChoice(bool spend)
    {
        // Preflight for the remote SQL/SignalR fixture. Original seats and the
        // finite QA command remain in this local journal; no database/API claim.
        var journal = new InstructionQaJournal();
        var session = new MatchSession("instruction-finite-qa", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await Cold();
        await session.EnsurePlayerAsync("P2", default); await Cold();
        var seeded = await session.SeedScenarioAsync("P1", "instruction-finite-source", "typed-power-payment-mixed-recycle",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "typed-power-payment-mixed-recycle" }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage); await Cold();
        await Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Select("RECYCLE_RUNE:P1-RUNE-RED-PARTIAL-PAYMENT-001");
        Assert.Equal(2, State().PendingRuleChoice!.Request.Preview!.RunePools["P1"].TotalPower);
        await Select(spend ? "SPEND_POWER:2" : "SPEND_POWER:0");
        Assert.Equal(spend ? 0 : 2, State().RunePools["P1"].TotalPower);
        Assert.Equal(spend ? 2 : 0, State().CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:typed-power-payment-mixed-recycle" },
            journal.Entries.Take(3).Select(e => e.CommandType));

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        Task Select(string option)
        {
            var request = State().PendingRuleChoice!.Request;
            return Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick;
            var result = await session.SubmitAsync(player, "instruction-qa-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(raw), default);
            Assert.True(result.Accepted, result.ErrorMessage); await Cold();
        }
        async Task Cold()
        {
            var last = journal.Entries[^1];
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
                e.CompletedTick, i, ev))).ToArray();
            var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick,
                last.CompletedEventSequence, p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
            var initial = MatchState.Create(last.RoomId) with { Seed = journal.Entries[0].OpeningSeed };
            var errors = MatchRecoveryValidator.Validate(last.RoomId, last.CompletedEventSequence, commands, events, views,
                last.AuthoritativeState, last.CompletedTick);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events,
                views, errors, last.AuthoritativeState, initial) { OpeningSeed = journal.Entries[0].OpeningSeed };
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            session = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        }
    }

    private sealed class InstructionQaJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrReplacedBattlefieldDoesNotRequestOrChargeTheInstruction(bool replacement)
    {
        var engine = new CoreRuleEngine();
        var played = await engine.ResolveAsync(Position(), new("instruction-field-boundary", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        // Synthetic target-incarnation boundary, not a claimed ordinary game.
        var cards = played.State.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        if (replacement) cards["FIELD"] = cards["FIELD"] with { ObjectGeneration = cards["FIELD"].ObjectGeneration + 1 };
        else cards.Remove("FIELD");
        var state = played.State with { CardObjects = cards };
        var events = new List<GameEvent>();
        for (var i = 0; i < 2; i++)
        {
            var result = await engine.ResolveAsync(state, new("invalid-field-pass-" + i, state.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State; events.AddRange(result.Events);
        }
        Assert.Null(state.PendingRuleChoice);
        Assert.Equal(2, state.RunePools["P1"].TotalPower);
        Assert.Equal(0, state.CardObjects["TARGET"].Damage);
        Assert.DoesNotContain(events, e => e.Kind == "COST_PAID");
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("stale")]
    [InlineData("unavailable")]
    [InlineData("multiple")]
    [InlineData("reduction")]
    public async Task InvalidInstructionPaymentsRejectAtomically(string branch)
    {
        var driver = await Open(Position());
        var state = driver.State;
        var request = state.PendingRuleChoice!.Request;
        var selected = branch switch {
            "unavailable" => "SPEND_POWER:3",
            "reduction" => "OPTIONAL_COST_REDUCTION:FORGED:0:ADDITIONAL:POWER-any",
            _ => "SPEND_POWER:1" };
        var command = new PayCostCommand(branch == "stale" ? "OLD" : request.Id, "RULE_REPLACEMENT",
            branch == "multiple" ? [selected, "SPEND_POWER:0"] : [selected]);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-instruction-payment", branch == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state)), MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state)), MatchStateHasher.HashValue(result.Prompts));
        await Decide(driver, "SPEND_POWER:0");
    }

    [Theory]
    [InlineData("payer")]
    [InlineData("kind")]
    [InlineData("option")]
    [InlineData("preview")]
    [InlineData("transcript")]
    public async Task ColdRecoveryRejectsForgedInstructionDecisions(string branch)
    {
        var driver = await Open(Position());
        var state = OfficialInsightAndSpellLockTests.Restore(driver.State);
        var pending = state.PendingRuleChoice!;
        var request = pending.Request;
        request = branch switch {
            "payer" => request with { PlayerId = "P2" },
            "kind" => request with { Kind = "UNRELATED" },
            "option" => request with { Options = [.. request.Options, new("SPEND_POWER:99", "伪造支付")] },
            "preview" => request with { Preview = request.Preview! with {
                RunePools = new Dictionary<string, RunePool>(request.Preview!.RunePools) { ["P1"] = new(0, 99) } } },
            _ => request };
        var forged = pending with { Request = request };
        if (branch == "transcript") forged = forged with { Answers = [new("FORGED", "SPEND_POWER:1")] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingRuleChoice = forged }));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    private static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, string[]? optional = null)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"], OptionalCosts: optional));
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.NotNull(driver.State.PendingRuleChoice);
        return driver;
    }

    private static Task Decide(StandbyDefendObserverTests.Driver driver, string option)
    {
        var pending = driver.State.PendingRuleChoice!;
        return driver.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option]));
    }

    internal static async Task<MatchState> ToPayment(MatchState played)
    {
        var engine = new CoreRuleEngine();
        var current = played;
        for (var i = 0; i < 2; i++)
        {
            var result = await engine.ResolveAsync(current,
                new("instruction-legacy-pass-" + i, current.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            current = result.State;
        }
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", current.PendingRuleChoice!.Request.Kind);
        return current;
    }

    internal static Task<ResolutionResult> Pay(MatchState state, string option)
    {
        var request = state.PendingRuleChoice!.Request;
        return new CoreRuleEngine().ResolveAsync(state, new("instruction-legacy-pay-" + state.Tick,
            request.PlayerId, CommandTypes.PayCost), new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]), default).AsTask();
    }

    private static MatchState WithPool(MatchState state, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };

    private static MatchState AddPermanent(MatchState state, string id, string card)
    {
        var source = P6TokenFactoryCatalog.TryGetByCardNo(card, out var token)
            ? token.CreateObject(id, "P1", "P1", false) : OfficialGarenConquestBattlefieldCountTests.Printed(id, card, "P1");
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append(id).ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = source },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "BASE") } };
    }

    private static MatchState Position()
    {
        var card = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", "OGN·268/298", "P1");
        var field = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·280/298", "P2") with { ControllerId = "P2" };
        var target = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·142/298", "P2");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target.CardNo!, out var targetBehavior));
        target = target with { Tags = target.Tags.Concat(targetBehavior.SourceUnitTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray() };
        return new("INSTRUCTION-POWER-COST", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(1, 2), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = card, ["FIELD"] = field, ["TARGET"] = target },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), ["TARGET"] = new("P2", "BATTLEFIELD", "FIELD") });
    }
}
