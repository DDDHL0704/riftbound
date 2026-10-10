using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

// Official CN 315.2.b, 316.3–4, 383.4.d, 390–392 and UNL judge FAQ.
public sealed class OfficialHoldSequenceTests
{
    [Fact]
    public async Task HeldDrawRespondsBeforeChannelAndNormalDrawAndDoesNotRepeatAfterRecovery()
    {
        var result = await Start(State("OGN·280/298"));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(MatchPhases.TurnStart, result.State.Phase);
        Assert.Equal("CHANNEL", result.State.TurnStartStep);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Single(result.State.StackItems);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
        result = await Drain(restored);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Null(result.State.TurnStartStep);
        Assert.Equal(2, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
    }

    [Theory]
    [InlineData("OGN·280/298", 2)]
    [InlineData("OGN·275/298", 1)]
    [InlineData("SFD·219/221", 0)]
    public async Task MandatoryHoldEffectsHaveRealStackWindows(string battlefield, int expectedHandAfterMain)
    {
        var start = await Start(State(battlefield));
        Assert.Single(start.State.StackItems);
        var result = await Drain(start.State);
        Assert.Equal(Math.Max(1, expectedHandAfterMain), result.State.PlayerZones["P1"].Hand.Count);
        if (battlefield == "OGN·275/298") Assert.Contains(result.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily) && c.IsExhausted);
        if (battlefield == "SFD·219/221") Assert.Empty(result.State.PlayerZones["P1"].RuneDeck);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlueSentinelAmplifiesUnitAndBattlefieldHoldsAndScheduledGainSurvivesSourceDeparture(bool removeSource)
    {
        var state = AddUnit(State("OGN·280/298"), "BLUE", "UNL-087/219", "F", "P1");
        state = AddUnit(state, "DRAWER", "SFD·027/221", "F", "P1");
        var start = await Start(state);
        Assert.Equal(4, start.State.TriggerQueue.Count);
        Assert.Equal(2, start.State.DelayedResourceGains.Count);
        var captured = start.State;
        if (removeSource)
        {
            var zones = captured.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
            zones["P1"] = zones["P1"] with { Battlefields = zones["P1"].Battlefields.Where(x => x != "BLUE").ToArray(), Graveyard = ["BLUE"] };
            var cards = captured.CardObjects.ToDictionary(e => e.Key, e => e.Value);
            cards["BLUE"] = cards["BLUE"] with { ControllerId = "P2", ObjectGeneration = cards["BLUE"].ObjectGeneration + 1 };
            captured = captured with { PlayerZones = zones, CardObjects = cards };
        }
        captured = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(captured))!;
        var result = await Drain(captured);
        Assert.Equal(7, result.State.PlayerZones["P1"].Hand.Count); // (1 + 2) * 2, then normal draw
        Assert.Equal(2, result.State.RunePools["P1"].Power); // after clearing the initial 9, not while paying a later card
        RetiredPaymentLedgerTests.AssertNoLedger(result.State);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Theory]
    [InlineData("OTHER", "P1")]
    [InlineData("F", "P2")]
    public async Task BlueAtAnotherLocationOrControlledByOpponentCannotAmplifyOwnHold(string field, string owner)
    {
        var start = await Start(AddUnit(State("OGN·280/298"), "BLUE", "UNL-087/219", field, owner));
        Assert.Single(start.State.StackItems);
        Assert.Empty(start.State.DelayedResourceGains);
        var result = await Drain(start.State);
        Assert.Equal(2, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalRuneCallPausesForPlayerAndCanBeDeclined(bool accept)
    {
        var start = await Start(State("OGN·288/298"));
        var response = start;
        var choice = Assert.IsType<PendingCardChoiceState>(response.State.PendingCardChoice);
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Empty(response.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, response.State.PlayerZones["P1"].RuneDeck.Count);
        var result = await Resolve(response.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            accept ? choice.LegalObjectIds.Take(1).ToArray() : []));
        Assert.True(result.Accepted, result.ErrorMessage);
        if (accept) {
            Assert.Equal(3, result.State.PlayerZones["P1"].RuneDeck.Count);
            result = await TurnSequenceTestDriver.Complete(result);
        }
        Assert.Equal(accept ? 0 : 1, result.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Single(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task HeldTriggerUsesCapturedControllerAndCardIdentity()
    {
        var state = AddUnit(State("OGN·294/298"), "DRAWER", "SFD·027/221", "F", "P1");
        var start = await Start(state);
        var cards = start.State.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards.Remove("DRAWER");
        var result = await Drain(start.State with { CardObjects = cards });
        Assert.Equal(3, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
    }

    [Theory]
    [InlineData("UNL-087/219")]
    [InlineData("UNL-087a/219")]
    public async Task BlueResourceIsGrantedAfterPoolClearAndCannotBeGrantedTwice(string no)
    {
        var start = await Start(AddUnit(State("OGN·280/298"), "BLUE", no, "F", "P1"));
        Assert.NotEmpty(start.State.DelayedResourceGains);
        var result = await TurnSequenceTestDriver.Complete(start);
        var events = result.Events.ToList();
        var clear = events.FindLastIndex(e => e.Kind == "RUNE_POOL_CLEARED");
        var gain = events.FindIndex(e => e.Kind == "POWER_GAINED");
        Assert.True(clear >= 0 && gain > clear);
        Assert.Equal(2, result.State.RunePools["P1"].Power);
        var again = await Resolve(result.State, new PassPriorityCommand());
        Assert.DoesNotContain(again.Events, e => e.Kind == "POWER_GAINED");
        Assert.Equal(2, again.State.RunePools["P1"].Power);
    }

    [Theory]
    [InlineData("SFD·027/221", 3, 0)]
    [InlineData("UNL-060/219", 2, 0)]
    [InlineData("SFD·089/221", 1, 1)]
    [InlineData("SFD·152/221", 1, 2)]
    public async Task UnitHoldEffectsResolveOnlyAfterResponses(string no, int cards, int tokens)
    {
        var start = await Start(AddUnit(State("OGN·294/298"), "SOURCE", no, "F", "P1"));
        Assert.Single(start.State.StackItems);
        Assert.Empty(start.State.PlayerZones["P1"].Hand);
        var result = await TurnSequenceTestDriver.Complete(start);
        Assert.Equal(cards, result.State.PlayerZones["P1"].Hand.Count);
        var created = result.Events.Where(e => e.Kind is "UNIT_TOKEN_CREATED" or "EQUIPMENT_TOKEN_CREATED").ToArray();
        Assert.Equal(tokens, created.Length);
        foreach (var e in created) Assert.True(result.State.CardObjects[(string)e.Payload["tokenObjectId"]!].IsExhausted);
    }

    [Theory]
    [InlineData("UNL-193/219", false)]
    [InlineData("UNL-193/219", true)]
    [InlineData("SFD·201/221", false)]
    [InlineData("SFD·201/221", true)]
    public async Task LegendExhaustCostRequiresAnExplicitChoice(string no, bool accept)
    {
        var state = AddLegend(State("OGN·294/298"), no);
        var result = await Start(state);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        var rejected = await Resolve(result.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]), "P2");
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(rejected.State));
        result = await Resolve(result.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : []));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(accept, result.State.CardObjects["LEGEND"].IsExhausted);
        if (accept) {
            Assert.Empty(result.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(result.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
            Assert.NotNull(result.State.PriorityPlayerId);
            result = await TurnSequenceTestDriver.Complete(result);
        }
        Assert.Equal(accept && no == "UNL-193/219" ? 2 : 1, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(accept && no == "SFD·201/221" ? 1 : 0, result.Events.Count(e => e.Kind == "EQUIPMENT_TOKEN_CREATED"));
    }

    [Fact]
    public async Task ControlledByOpponentLegendCannotTriggerForItsOwnersHold()
    {
        var state = AddLegend(State("OGN·294/298"), "UNL-193/219");
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["LEGEND"] = cards["LEGEND"] with { ControllerId = "P2" };
        var result = await Start(state with { CardObjects = cards });
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Single(result.State.PlayerZones["P1"].Hand);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnergyHubPaymentPausesStartAndSpendsOnlyWhenChosen(bool pay)
    {
        var start = await Start(State("SFD·214/221"));
        var response = start;
        var payment = Assert.IsType<PendingPaymentState>(response.State.PendingPayment);
        Assert.Equal("CHANNEL", response.State.TurnStartStep);
        Assert.Equal(1, response.State.PlayerScores["P1"]);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(response.State))!;
        var result = await Resolve(restored, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal(pay ? 1 : 0, result.Events.Count(e => e.Kind == "COST_PAID"));
        if (pay) result = await TurnSequenceTestDriver.Complete(result);
        Assert.Equal(pay ? 2 : 1, result.State.PlayerScores["P1"]);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        var repeat = await Resolve(result.State, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.False(repeat.Accepted);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(repeat.State));
    }

    [Fact]
    public async Task EnergyHubInsufficientOrWrongPlayerPaymentDoesNotAdvanceOrMutate()
    {
        var state = State("SFD·214/221") with { RunePools = new Dictionary<string,RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var response = await Start(state);
        var payment = response.State.PendingPayment!;
        foreach (var player in new[] { "P1", "P2" }) {
            var result = await Resolve(response.State, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]), player);
            Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(response.State), MatchStateHasher.Hash(result.State));
        }
    }

    [Fact]
    public async Task BoonChoiceCannotTargetUnrelatedBattlefield()
    {
        var state = AddUnit(AddUnit(State("OGN·283/298"), "HERE", "SFD·125/221", "F", "P1"), "ELSEWHERE", "SFD·125/221", "OTHER", "P1");
        var response = await Start(state);
        var choice = response.State.PendingCardChoice!;
        Assert.Equal(new[] { "HERE" }, choice.LegalObjectIds);
        var bad = await Resolve(response.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ELSEWHERE"]));
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(response.State), MatchStateHasher.Hash(bad.State));
        var good = await Resolve(response.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HERE"]));
        Assert.True(good.Accepted, good.ErrorMessage);
        Assert.DoesNotContain(good.Events, e => e.Kind == "BOON_GRANTED");
        good = await TurnSequenceTestDriver.Complete(good);
        Assert.Contains(good.Events, e => e.Kind == "BOON_GRANTED" && Equals(e.Payload["targetObjectId"], "HERE"));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public async Task GrandPlazaTestsItsConditionWhenHoldOccurs(int count)
    {
        var state = State("OGN·293/298");
        for (var i = 0; i < count; i++) state = AddUnit(state, "U" + i, "SFD·125/221", "F", "P1");
        var start = await Start(state);
        Assert.Equal(count == 7 ? 1 : 0, start.State.StackItems.Count);
        var result = await TurnSequenceTestDriver.Complete(start);
        Assert.Equal(count == 7 ? "P1" : null, result.State.WinnerPlayerId);
    }

    [Fact]
    public async Task DelayedStateRecoveryRejectsForgedAmountButAllowsSourceDeparture()
    {
        var captured = (await Start(AddUnit(State("OGN·280/298"), "BLUE", "UNL-087/219", "F", "P1"))).State;
        var errors = MatchRecoveryValidator.Validate(captured.RoomId, 0, [], [], new Dictionary<string,RecoveredPlayerView>(), captured, captured.Tick);
        Assert.Empty(errors);
        var forged = captured with { DelayedResourceGains = captured.DelayedResourceGains.Select(g => g with { Power = 99 }).ToArray() };
        Assert.Contains(MatchRecoveryValidator.Validate(forged.RoomId, 0, [], [], new Dictionary<string,RecoveredPlayerView>(), forged, forged.Tick), e => e.Contains("invalid captured delayed resource"));
    }

    [Fact]
    public async Task OpponentCanActuallyPlayReactionDuringTurnStartAndHoldStillResumes()
    {
        var state = AddUnit(State("OGN·280/298"), "TARGET", "SFD·125/221", "F", "P1");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SPELL"] = new("SPELL", cardNo: "OGN·095/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P2"] = zones["P2"] with { Hand = ["SPELL"], MainDeck = ["P2-D1", "P2-D2"] };
        cards["P2-D1"] = new("P2-D1", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
        cards["P2-D2"] = cards["P2-D1"] with { ObjectId = "P2-D2" };
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value); locations["SPELL"] = new("P2", "HAND");
        var start = await Start(state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations });
        var passed = await Resolve(start.State, new PassPriorityCommand());
        Assert.Equal("P2", passed.State.PriorityPlayerId);
        var request = new PlayCostPreviewRequestDto("start-reaction", passed.Prompts["P2"].PromptId!, passed.State.Tick,
            new PlayCardCommand("SPELL", "OGN·095/298", ["TARGET"]));
        var quote = new CoreRuleEngine().PreviewPlayCard(passed.State, "P2", request);
        Assert.True(quote.IsValid, quote.Message);
        Assert.True(quote.CanPay, quote.Message);
        var played = await Resolve(passed.State, request.Command, "P2");
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(2, played.State.StackItems.Count);
        Assert.Equal("CHANNEL", played.State.TurnStartStep);
        var completed = await TurnSequenceTestDriver.Complete(played);
        Assert.Equal(MatchPhases.Main, completed.State.Phase);
        Assert.Equal(2, completed.State.PlayerZones["P1"].Hand.Count);
    }

    [Theory]
    [InlineData("OGN·280/298")]
    [InlineData("OGN·288/298")]
    [InlineData("SFD·214/221")]
    public async Task RealSessionStartChoicesRecoverAndReplayToIdenticalState(string battlefield)
    {
        var initial = State(battlefield);
        var journal = new HoldJournal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command)
        {
            var result = await session.SubmitAsync(player, Guid.NewGuid().ToString(), command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Empty(MatchRecoveryValidator.Validate(result.State.RoomId, 0, [], [], new Dictionary<string,RecoveredPlayerView>(), result.State, result.State.Tick));
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
            return result;
        }
        var result = await Submit("P1", new PassPriorityCommand());
        result = await TurnSequenceTestDriver.Complete(result, Submit);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_HOLD_EVIDENCE") is { Length: > 0 } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, battlefield.Replace('·', '-').Replace('/', '-') + "-replay.json"), JsonSerializer.Serialize(replay));
            foreach (var entry in journal.Entries)
                foreach (var player in initial.Seats.Keys) {
                    File.WriteAllText(Path.Combine(root, $"{battlefield.Replace('·', '-').Replace('/', '-')}-{entry.CompletedTick}-{player}-snapshot.json"), JsonSerializer.Serialize(entry.Snapshots[player], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                }
        }
    }
    [Fact]
    public async Task AmplifiedAcademyHoldGrantsTwoIndependentEchoes()
    {
        var state = AddUnit(State("UNL-216/219"), "BLUE", "UNL-087/219", "F", "P1");
        var result = await Drain((await Start(state)).State);
        Assert.Equal(2, result.State.UntilEndOfTurnEffects.Count(effect => EchoCostRules.IsGrant(effect, "P1")));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-061/219", out var behavior));
        Assert.Equal(3, EchoCostRules.Available(result.State, "P1", behavior).Count);
    }

    private sealed class HoldJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    internal static MatchState AddLegend(MatchState state, string no)
    {
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["LEGEND"] = new("LEGEND", cardNo: no, ownerId: "P1", controllerId: "P1");
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"] };
        var locations = state.ObjectLocations.ToDictionary(x => x.Key, x => x.Value); locations["LEGEND"] = new("P1", "LEGEND");
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static async Task<ResolutionResult> Drain(MatchState state)
    {
        ResolutionResult? result = null;
        for (var i = 0; i < 40; i++)
        {
            if (state.PendingCardChoice is not null || state.PendingPayment is not null || state.Phase == MatchPhases.Main) break;
            GameCommand command = state.TriggerQueue.Count > 1
                ? new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()) : new PassPriorityCommand();
            result = await Resolve(state, command, state.PriorityPlayerId ?? state.TurnPlayerId);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
        }
        Assert.NotNull(result); return result;
    }
    private static ValueTask<ResolutionResult> Start(MatchState state) => Resolve(state, new PassPriorityCommand());
    private static ValueTask<ResolutionResult> Resolve(MatchState state, GameCommand command, string player = "P1") =>
        new CoreRuleEngine().ResolveAsync(state, new(Guid.NewGuid().ToString(), player, command.CmdType), command, default);
    internal static MatchState AddUnit(MatchState state, string id, string no, string field, string controller)
    {
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards[id] = new(id, cardNo: no, power: 3, tags: [CardObjectTags.UnitCard], ownerId: controller, controllerId: controller);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones[controller] = zones[controller] with { Battlefields = zones[controller].Battlefields.Concat([id]).ToArray() };
        var positions = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        positions[id] = new(controller, "BATTLEFIELD", field);
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = positions };
    }
    internal static MatchState State(string battlefield)
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["F"] = new("F", cardNo: battlefield, tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1"),
            ["OTHER"] = new("OTHER", cardNo: "OGN·294/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2") };
        var positions = new Dictionary<string, ObjectLocationState> { ["F"] = new("P1", "BATTLEFIELD", "F"), ["OTHER"] = new("P2", "BATTLEFIELD", "OTHER") };
        var deck = Enumerable.Range(0, 12).Select(i => "D" + i).ToArray();
        foreach (var id in deck) { cards[id] = new(id, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"); positions[id] = new("P1", "MAIN_DECK"); }
        var runes = new[] { "R1", "R2", "R3" };
        foreach (var id in runes) { cards[id] = new(id, cardNo: "OGN·007/298", tags: [CardObjectTags.RuneCard, "COLOR:red"], ownerId: "P1", controllerId: "P1"); positions[id] = new("P1", "RUNE_DECK"); }
        return new("HOLD-TEST", 0, 5, "P1", new Dictionary<string,string> { ["P1"]="P1", ["P2"]="P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.TurnStart, timingState: TimingStates.NeutralClosed,
            cardObjects: cards, objectLocations: positions,
            runePools: new Dictionary<string,RunePool> { ["P1"] = new(7,9), ["P2"] = new(3,4) },
            playerZones: new Dictionary<string,PlayerZones> { ["P1"] = PlayerZones.Empty with { MainDeck = deck, RuneDeck = runes, Battlefields = ["F"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER"] } });
    }
}
