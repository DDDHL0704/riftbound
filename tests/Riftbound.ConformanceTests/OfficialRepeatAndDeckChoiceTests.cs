using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialRepeatAndDeckChoiceTests
{
    [Theory]
    [InlineData("BASE_UNIT_DAMAGE_4", "U1", "DESTROY_EQUIPMENT", "E1")]
    [InlineData("DESTROY_EQUIPMENT", "E1", "BASE_UNIT_DAMAGE_4", "U1")]
    [InlineData("DESTROY_EQUIPMENT", "E1", "DESTROY_EQUIPMENT", "E2")]
    [InlineData("BASE_UNIT_DAMAGE_4", "U1", "BASE_UNIT_DAMAGE_4", "U2")]
    public async Task RocketBarrageChoosesModesAndTargetsPerExecution(string firstMode, string firstTarget, string mode, string target)
    {
        var state = State("SFD·077/221");
        var command = new PlayCardCommand("CARD", "SFD·077/221", [firstTarget], firstMode, ["ECHO"],
            RepeatChoices: [new(mode, [target])]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(8, quote.Cost!.Mana);
        var played = await Resolve(state, command);
        Assert.Single(played.State.StackItems);
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(played.State))!;
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(recovered));
        var result = await Drain(recovered);
        Assert.Empty(result.State.StackItems);
        foreach (var id in new[] { firstTarget, target })
        {
            if (id.StartsWith("E")) Assert.Contains(id, result.State.PlayerZones["P2"].Graveyard);
            else Assert.Equal(4, result.State.CardObjects[id].Damage);
        }
        Assert.Equal(2, result.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Single(result.Events, e => e.Kind == "STACK_ITEM_RESOLVED");
        Assert.Single(result.State.PlayerZones["P1"].Graveyard, id => id == "CARD");
    }

    [Theory]
    [InlineData("DESTROY_EQUIPMENT", "U1")]
    [InlineData("BASE_UNIT_DAMAGE_4", "E1")]
    [InlineData("FAKE", "U1")]
    public async Task IllegalRepeatChoiceDoesNotSpendOrChangeState(string mode, string target)
    {
        var state = State("SFD·077/221");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", "SFD·077/221", ["U1"], "BASE_UNIT_DAMAGE_4", ["ECHO"], RepeatChoices: [new(mode, [target])]), default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task RepeatTargetThatChangedGenerationIsNotHit()
    {
        var result = await Resolve(State("SFD·077/221"), new PlayCardCommand("CARD", "SFD·077/221", ["U1"], "BASE_UNIT_DAMAGE_4", ["ECHO"],
            RepeatChoices: [new("BASE_UNIT_DAMAGE_4", ["U2"])]));
        var state = result.State with { CardObjects = new Dictionary<string, CardObjectState>(result.State.CardObjects)
            { ["U2"] = result.State.CardObjects["U2"] with { ObjectGeneration = 1 } } };
        result = await Drain(state);
        Assert.Equal(4, result.State.CardObjects["U1"].Damage);
        Assert.Equal(0, result.State.CardObjects["U2"].Damage);
    }

    [Fact]
    public async Task PredictiveOffensiveLooksOnlyAfterResponseThenRepeatsWithTheNewTopCards()
    {
        var state = State("SFD·122/221");
        var promptBefore = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        Assert.DoesNotContain("\"D1\"", promptBefore);
        var played = await Resolve(state, new PlayCardCommand("CARD", "SFD·122/221", [], OptionalCosts: ["ECHO"]));
        Assert.Null(played.State.PendingCardChoice);
        var pending = await Drain(played.State);
        var choice = Assert.IsType<PendingCardChoiceState>(pending.State.PendingCardChoice);
        Assert.Equal(["D1", "D2"], choice.ContextObjectIds);
        Assert.Single(pending.State.StackItems);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(pending.Prompts["P2"]));
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(pending.Snapshots["P2"]));
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(pending.State))!;
        Assert.Equal(MatchStateHasher.Hash(pending.State), MatchStateHasher.Hash(recovered));
        var rejected = await new CoreRuleEngine().ResolveAsync(recovered, new("other", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1"]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(recovered), MatchStateHasher.Hash(rejected.State));
        var first = await Resolve(recovered, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D2"]));
        Assert.Contains("D2", first.State.PlayerZones["P1"].Hand);
        choice = Assert.IsType<PendingCardChoiceState>(first.State.PendingCardChoice);
        Assert.Equal(["D3", "D4"], choice.ContextObjectIds);
        var second = await Resolve(first.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D3"]));
        Assert.Null(second.State.PendingCardChoice);
        Assert.Empty(second.State.StackItems);
        Assert.Equal(["D2", "D3"], second.State.PlayerZones["P1"].Hand);
        Assert.Equal(["D1", "D4"], second.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("CARD", second.State.PlayerZones["P1"].Graveyard);
    }

    [Theory]
    [InlineData("UNL-032/219", true)]
    [InlineData("OGN·183/298", false)]
    public async Task FilteredChoiceRevealsButPutInHandIsNotDraw(string card, bool reveal)
    {
        var played = await Resolve(State(card), new PlayCardCommand("CARD", card, []));
        var pending = await Drain(played.State);
        var choice = Assert.IsType<PendingCardChoiceState>(pending.State.PendingCardChoice);
        var result = await Resolve(pending.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1"]));
        Assert.Equal(reveal, result.Events.Any(e => e.Kind == "CARD_REVEALED"));
        Assert.Equal(reveal, result.Events.Any(e => e.Kind == "CARD_DRAWN"));
        Assert.Equal(!reveal, result.Events.Any(e => e.Kind == "CARD_ADDED_TO_HAND"));
        if (reveal && Environment.GetEnvironmentVariable("RIFTBOUND_MULTI_EVIDENCE") is { Length: > 0 } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "deck-optional-prompt.json"), JsonSerializer.Serialize(pending.Prompts["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var path = Path.Combine(root, "native-deck-decline-command.json");
            if (File.Exists(path))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var declined = await Resolve(pending.State, GameCommandJsonMapper.Map(json.RootElement));
                Assert.Empty(declined.State.PlayerZones["P1"].Hand);
                Assert.Null(declined.State.PendingCardChoice);
                Assert.Equal(4, declined.State.PlayerZones["P1"].MainDeck.Count);
            }
        }
    }

    [Theory]
    [InlineData("SFD·077/221")]
    [InlineData("SFD·122/221")]
    public async Task NativeCommandsAndEveryContinuationReplayWithPrivatePrompts(string card)
    {
        var state = State(card);
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var session = new MatchSession(state, engine, journal);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MULTI_EVIDENCE");
        var repeat = card == "SFD·077/221";
        var command = repeat
            ? new PlayCardCommand("CARD", card, ["U1"], "BASE_UNIT_DAMAGE_4", ["ECHO"], RepeatChoices: [new("DESTROY_EQUIPMENT", ["E1"])])
            : new PlayCardCommand("CARD", card, [], OptionalCosts: ["ECHO"]);
        if (!string.IsNullOrEmpty(root))
        {
            Directory.CreateDirectory(root);
            if (repeat)
            {
                var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
                File.WriteAllText(Path.Combine(root, "native-candidate.json"), JsonSerializer.Serialize(candidate, json));
                File.WriteAllText(Path.Combine(root, "native-quote.json"), JsonSerializer.Serialize(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)), json));
                if (File.Exists(Path.Combine(root, "native-command.json")))
                {
                    using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-command.json")));
                    var mapped = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(native.RootElement));
                    Assert.Equal(command.Mode, mapped.Mode);
                    Assert.Equal(command.TargetObjectIds, mapped.TargetObjectIds);
                    Assert.Equal(command.RepeatChoices![0].Mode, mapped.RepeatChoices![0].Mode);
                    Assert.Equal(command.RepeatChoices[0].TargetObjectIds, mapped.RepeatChoices[0].TargetObjectIds);
                    command = mapped;
                }
            }
        }
        async Task<ResolutionResult> Submit(string player, GameCommand action)
        {
            var result = await session.SubmitAsync(player, "multi-" + journal.Entries.Count, action,
                JsonSerializer.SerializeToElement(action, action.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
            Assert.Empty(MatchRecoveryValidator.Validate(restored.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), restored, restored.Tick));
            return result;
        }
        var result = await Submit("P1", command);
        for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        if (!repeat)
        {
            var hiddenView = JsonSerializer.SerializeToElement(result.Snapshots["P2"].Timing["pendingCardChoice"]);
            Assert.False(hiddenView.TryGetProperty("legalCount", out _));
            Assert.False(hiddenView.TryGetProperty("maxCount", out _));
            if (!string.IsNullOrEmpty(root)) File.WriteAllText(Path.Combine(root, "deck-choice-prompt.json"), JsonSerializer.Serialize(result.Prompts["P1"], json));
            Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(result.Prompts["P2"]));
            Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(result.Snapshots["P2"]));
            var choice = result.State.PendingCardChoice!;
            var forged = result.State with { PendingCardChoice = choice with { RequiredCount = 0 } };
            Assert.Contains(MatchRecoveryValidator.Validate(forged.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), forged, forged.Tick), e => e.Contains("deck choice permissions"));
            GameCommand select = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D2"]);
            if (!string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, "native-deck-choice-command.json")))
            {
                using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-deck-choice-command.json")));
                select = GameCommandJsonMapper.Map(native.RootElement);
            }
            result = await Submit("P1", select);
            choice = result.State.PendingCardChoice!;
            result = await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D3"]));
            Assert.Equal(["D2", "D3"], result.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain("\"D3\"", JsonSerializer.Serialize(result.Events));
            Assert.All(result.Events.Where(e => e.Kind == "CARDS_RECYCLED"), e => Assert.False(e.Payload.ContainsKey("cardIds")));
        }
        Assert.Empty(result.State.StackItems);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (!string.IsNullOrEmpty(root)) File.WriteAllText(Path.Combine(root, repeat ? "repeat-replay.json" : "deck-replay.json"), JsonSerializer.Serialize(replay, json));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LethalDamageWaitsUntilAllInstructionsInTheSingleItemComplete(bool explicitChoices)
    {
        var state = State("SFD·077/221");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["U1"] = state.CardObjects["U1"] with { Power = 4 } } };
        var result = await Resolve(state, new PlayCardCommand("CARD", "SFD·077/221", ["U1"], "BASE_UNIT_DAMAGE_4", ["ECHO"],
            RepeatChoices: explicitChoices ? [new("BASE_UNIT_DAMAGE_4", ["U1"])] : null));
        result = await Drain(result.State);
        var events = result.Events.ToList();
        Assert.Equal(2, events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Single(events, e => e.Kind == "UNIT_DESTROYED");
        Assert.True(events.FindIndex(e => e.Kind == "UNIT_DESTROYED") > events.FindLastIndex(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Contains("U1", result.State.PlayerZones["P2"].Graveyard);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"mode\":\"DESTROY_EQUIPMENT\",\"targetObjectIds\":123}]")]
    public async Task MalformedRepeatPayloadIsRejectedInsteadOfSilentlyChangingItsMeaning(string repeats)
    {
        using var json = JsonDocument.Parse("{\"cmdType\":\"PLAY_CARD\",\"repeatChoices\":" + repeats + "}");
        var command = Assert.IsType<UnsupportedCommand>(GameCommandJsonMapper.Map(json.RootElement));
        var state = State("SFD·077/221");
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad-payload", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("OGN·080/298")]
    public void UnsupportedRepeatSurfacesCannotOfferAChargedPartialResolution(string cardNo)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.False(EchoCostRules.SupportsRepeatResolution(behavior));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static async Task<ResolutionResult> Resolve(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("test-" + state.Tick, "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }
    private static async Task<ResolutionResult> Drain(MatchState state)
    {
        ResolutionResult result = null!;
        for (var i = 0; i < 2; i++)
        {
            result = await new CoreRuleEngine().ResolveAsync(state, new("pass-" + i, state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
        }
        return result;
    }
    private static MatchState State(string card)
    {
        var objects = new Dictionary<string, CardObjectState>
        {
            ["CARD"] = new("CARD", cardNo: card, ownerId: "P1", controllerId: "P1"),
            ["U1"] = new("U1", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 10, tags: [CardObjectTags.UnitCard]),
            ["U2"] = new("U2", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 10, tags: [CardObjectTags.UnitCard]),
            ["E1"] = new("E1", cardNo: "SFD·190/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard]),
            ["E2"] = new("E2", cardNo: "SFD·190/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard])
        };
        foreach (var id in new[] { "D1", "D2", "D3", "D4" })
            objects[id] = new(id, cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
        return new("REPEAT-CHOICE", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], MainDeck = ["D1", "D2", "D3", "D4"] },
                ["P2"] = PlayerZones.Empty with { Base = ["U1", "U2", "E1", "E2"] } }, cardObjects: objects);
    }
}
