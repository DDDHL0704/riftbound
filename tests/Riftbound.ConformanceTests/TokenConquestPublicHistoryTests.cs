using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 186.1 removes a departing token. Its earlier public participation must
// remain a historical binding, rather than requiring a live object forever.
public sealed class TokenConquestPublicHistoryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutomaticAndSubmittedCombatRetainDeletedTokenConquestParticipants(bool manual, bool secondToken)
    {
        var initial = Position();
        if (manual) initial = AddDefender(initial, secondToken);
        var driver = Driver(initial, $"combat-{manual}-{secondToken}");
        await Start(driver, manual);
        if (manual)
        {
            await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", [new("A", "D", 1), new("A", "D2", 7)]));
            await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("D", "A", 1), new("D2", "A", 1)]));
        }
        await Finish(driver);
        Assert.False(driver.State.CardObjects.ContainsKey("D"));
        Assert.DoesNotContain("D", driver.State.PlayerZones["P2"].Graveyard);
        if (secondToken) Assert.False(driver.State.CardObjects.ContainsKey("D2"));
        AssertHistory(driver.State, manual ? ["A", "D", "D2"] : ["A", "D"]);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("UNL-114/219")]
    [InlineData("SFD·123/221")]
    public async Task ResultTriggerContinuationRetainsTheSameDeletedTokenBinding(string winner)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["A"] = initial.CardObjects["A"] with { CardNo = winner }
        } };
        var driver = Driver(initial, "result-trigger-" + winner.Replace('/', '-').Replace('·', '-'));
        await Start(driver);
        Assert.NotNull(driver.State.PendingBattleResult);
        Assert.False(driver.State.CardObjects.ContainsKey("D"));
        Assert.Equal(0, driver.State.PlayerScores.GetValueOrDefault("P1"));
        await Finish(driver);
        Assert.Contains("HISTORY-DRAW", driver.State.PlayerZones["P1"].Hand);
        AssertHistory(driver.State, ["A"]);
        var battle = Assert.Single(driver.State.BattleResolutions);
        Assert.Equal(new[] { "A", "D" }, battle.PublicObjectBindings!.Select(b => b.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(battle.PublicObjectBindings!, b => Assert.Equal(0, b.Generation));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("RECALL")]
    [InlineData("BANISH")]
    public async Task SynchronousDestructionChoiceKeepsTheOriginalPublicTokenIncarnation(string choice)
    {
        var initial = DestructionOrderTests.Gear(Position());
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D"] = initial.CardObjects["D"] with { UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN"] }
        } };
        var driver = Driver(initial, "replacement-" + choice);
        await Start(driver);
        var request = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice).Request;
        var option = choice == "RECALL" ? "GEAR:D:G1" : "BANISH:D";
        Assert.Contains(request.Options, o => o.Id == option);
        await driver.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        await Finish(driver);
        if (choice == "RECALL") Assert.Contains("D", driver.State.PlayerZones["P2"].Base);
        else Assert.False(driver.State.CardObjects.ContainsKey("D"));
        AssertHistory(driver.State, ["A", "D"]);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task MutuallyDestroyedTokensRetainBattleHistoryWithoutInventingConquest()
    {
        var initial = Position();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var token));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["A"] = token.CreateObject("A", "P1", "P1")
        } };
        var driver = Driver(initial, "both-tokens-destroyed");
        await Start(driver);
        await Finish(driver);
        Assert.All(new[] { "A", "D" }, id => Assert.False(driver.State.CardObjects.ContainsKey(id)));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal(0, driver.State.PlayerScores.GetValueOrDefault("P1"));
        var battle = Assert.Single(driver.State.BattleResolutions);
        Assert.Equal("NO_RESULT", battle.Kind);
        Assert.Equal(new[] { "A", "D" }, battle.PublicObjectBindings!.Select(b => b.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(battle.PublicObjectBindings!, b => Assert.Equal(0, b.Generation));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("PRIVATE-HAND")]
    [InlineData("PRIVATE-DECK")]
    [InlineData("PRIVATE-RUNE")]
    [InlineData("DUPLICATE")]
    [InlineData("FUTURE-GENERATION")]
    [InlineData("NONPARTICIPANT")]
    public async Task ColdValidationRejectsForgedPrivateOrInvalidConquestBindings(string attack)
    {
        var initial = Position(token: false);
        var driver = Driver(initial, "forged-" + attack);
        await Start(driver);
        await Finish(driver);
        var history = Assert.Single(driver.State.BattlefieldResolutions, r => r.Kind == "CONQUERED");
        var bindings = history.PublicObjectBindings!.ToList();
        var participants = history.ParticipantObjectIds.ToList();
        if (attack.StartsWith("PRIVATE-", StringComparison.Ordinal))
        {
            participants.Add(attack);
            bindings.Add(new(attack, driver.State.CardObjects[attack].ObjectGeneration));
        }
        else if (attack == "DUPLICATE") bindings.Add(bindings[0]);
        else if (attack == "FUTURE-GENERATION") bindings = bindings.Select(b => b.ObjectId == "D" ? b with { Generation = 100 } : b).ToList();
        else bindings.Add(new("PRIVATE-HAND", 0));
        var forgedHistory = history with { PublicObjectBindings = bindings, ParticipantObjectIds = participants };
        var forged = driver.State with { BattlefieldResolutions = driver.State.BattlefieldResolutions.Select(r => r == history ? forgedHistory : r).ToArray() };
        forged = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(forged))!;
        Assert.Contains("invalid captured public battlefield history reference", OfficialInsightAndSpellLockTests.Errors(forged));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("participant")]
    public async Task ReprojectedHistoryCannotSubstituteOrRedactCapturedPublicIds(string field)
    {
        var initial = Position(token: false);
        var driver = Driver(initial, "forged-view-" + field);
        await Start(driver);
        await Finish(driver);
        var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state).ToDictionary(e => e.Key, e => e.Value);
        var snapshot = snapshots["P1"];
        var timing = new Dictionary<string, object?>(snapshot.Timing);
        var histories = JsonSerializer.SerializeToElement(timing["battlefieldResolutions"], Json)
            .Deserialize<List<Dictionary<string, JsonElement>>>(Json)!;
        var row = Assert.Single(histories, h => h["kind"].GetString() == "CONQUERED");
        if (field == "source") row["sourceObjectId"] = JsonSerializer.SerializeToElement("PRIVATE-HAND");
        else row["participantObjectIds"] = JsonSerializer.SerializeToElement(new[] { "REDACTED", "A" });
        timing["battlefieldResolutions"] = JsonSerializer.SerializeToElement(histories, Json);
        snapshots["P1"] = snapshot with { Timing = timing };
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = snapshots.ToDictionary(e => e.Key, e => new RecoveredPlayerView(e.Key, state.Tick, 0,
            e.Value, state.Tick, 0, prompts[e.Key]));
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state));
        Assert.Contains(errors, e => e.Contains(field == "source" ? "public historical source" : "public historical participants", StringComparison.Ordinal));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReturnToHandKeepsTheEarlierPublicGenerationButRejectsTheCurrentPrivateOne(bool token)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(token), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "old-public-after-return-" + token);
        await Start(driver);
        await Finish(driver);
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["A"]));
        await driver.Top();
        Assert.Contains("A", driver.State.PlayerZones["P1"].Hand);
        var history = Assert.Single(driver.State.BattlefieldResolutions, r => r.Kind == "CONQUERED");
        Assert.Equal(0, Assert.Single(history.PublicObjectBindings!, b => b.ObjectId == "A").Generation);
        Assert.True(driver.State.CardObjects["A"].ObjectGeneration > 0);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        var forgedHistory = history with { PublicObjectBindings = history.PublicObjectBindings!.Select(b => b.ObjectId == "A"
            ? b with { Generation = driver.State.CardObjects["A"].ObjectGeneration } : b).ToArray() };
        var forged = driver.State with { BattlefieldResolutions = driver.State.BattlefieldResolutions.Select(r => r == history ? forgedHistory : r).ToArray() };
        Assert.Contains("invalid captured public battlefield history reference", OfficialInsightAndSpellLockTests.Errors(forged));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("UNRECORDED-DEFENDER")]
    [InlineData("PHANTOM-TOKEN")]
    public async Task CoherentlyForgedHistoryCannotPassOriginalJournalRecovery(string attack)
    {
        // Reuse the tracked dev-seed journal driver: real room seats and seed
        // commands permit the full startup replay audit, unlike a midgame frame.
        var run = await BattleResultContinuationRecoveryTests.Open("UNL-114/219");
        for (var i = 0; run.State.PendingBattleResult is not null || run.State.StackItems.Count > 0; i++)
        {
            Assert.InRange(i, 0, 9);
            await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        var history = Assert.Single(run.State.BattlefieldResolutions, r => r.Kind == "CONQUERED");
        var id = attack == "UNRECORDED-DEFENDER" ? "DEFENDER" : "DELETED-TOKEN-NEVER-CREATED";
        Assert.DoesNotContain(id, history.ParticipantObjectIds);
        var forgedHistory = history with { ParticipantObjectIds = [..history.ParticipantObjectIds, id],
            PublicObjectBindings = [..history.PublicObjectBindings!, new(id, 0)] };
        var forged = run.State with { BattlefieldResolutions = run.State.BattlefieldResolutions.Select(r => r == history ? forgedHistory : r).ToArray() };
        var frame = run.Frame(forged);
        Assert.Empty(frame.ValidationErrors);
        var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(replayErrors, e => e.Contains("final state hash", StringComparison.Ordinal));
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new HistoryStore(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        run.Save("conquest-history-" + attack, new { forgedFrame = frame, replayErrors, error.Code });
    }

    private sealed class HistoryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private static MatchState Position(bool token = true)
    {
        var state = OfficialLegendConquestTests.CombatPosition("OGN·269/298", power: 8);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["D"] = token ? factory.CreateObject("D", "P2", "P2") : state.CardObjects["D"] with { Power = 1 },
            ["HISTORY-DRAW"] = new("HISTORY-DRAW", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard]),
            ["PRIVATE-HAND"] = new("PRIVATE-HAND", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 2, tags: [CardObjectTags.UnitCard]),
            ["PRIVATE-DECK"] = new("PRIVATE-DECK", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 2, tags: [CardObjectTags.UnitCard]),
            ["PRIVATE-RUNE"] = new("PRIVATE-RUNE", cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:red"])
        };
        return state with
        {
            Tick = 0, CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = ["HISTORY-DRAW"] },
                ["P2"] = state.PlayerZones["P2"] with { Hand = [..state.PlayerZones["P2"].Hand, "PRIVATE-HAND"], MainDeck = ["PRIVATE-DECK"], RuneDeck = ["PRIVATE-RUNE"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            {
                ["HISTORY-DRAW"] = new("P1", "MAIN_DECK"), ["PRIVATE-HAND"] = new("P2", "HAND"),
                ["PRIVATE-DECK"] = new("P2", "MAIN_DECK"), ["PRIVATE-RUNE"] = new("P2", "RUNE_DECK")
            }
        };
    }

    private static MatchState AddDefender(MatchState state, bool token)
        => state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["D2"] = state.CardObjects["D"] with { ObjectId = "D2", CardNo = token ? "OGN·271/298" : "SFD·125/221",
                    TokenFactoryCardNo = token ? "OGN·271/298" : null, Tags = token ? state.CardObjects["D"].Tags : [CardObjectTags.UnitCard] }
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = [..state.PlayerZones["P2"].Battlefields, "D2"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["D2"] = new("P2", "BATTLEFIELD", "BF") }
        };

    private static async Task Start(StandbyDefendObserverTests.Driver driver, bool manual = false)
    {
        await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("BF", ["A"], manual ? ["D", "D2"] : ["D"], ["COMBAT_ASSIGNMENT"]));
    }

    private static async Task Finish(StandbyDefendObserverTests.Driver driver)
    {
        for (var i = 0; driver.State.PendingBattleResult is not null || driver.State.StackItems.Count > 0; i++)
        {
            Assert.InRange(i, 0, 5);
            await driver.Top();
        }
    }

    private static void AssertHistory(MatchState state, string[] participants)
    {
        Assert.Equal("P1", state.BattlefieldStates["BF"].ControllerId);
        Assert.Equal(1, state.PlayerScores["P1"]);
        var conquered = Assert.Single(state.BattlefieldResolutions, r => r.Kind == "CONQUERED");
        Assert.Equal(participants.Order(StringComparer.Ordinal), conquered.ParticipantObjectIds.Order(StringComparer.Ordinal));
        Assert.Equal(participants.Order(StringComparer.Ordinal), conquered.PublicObjectBindings!.Select(b => b.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(conquered.PublicObjectBindings!, b => Assert.Equal(0, b.Generation));
        Assert.DoesNotContain(conquered.PublicObjectBindings!, b => b.ObjectId.StartsWith("PRIVATE-", StringComparison.Ordinal));
        foreach (var viewer in new[] { "P1", "P2" })
        {
            var snapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[viewer], Json);
            var history = snapshot.GetProperty("timing").GetProperty("battlefieldResolutions").EnumerateArray().Single(h => h.GetProperty("kind").GetString() == "CONQUERED");
            Assert.Equal(participants.Order(StringComparer.Ordinal), history.GetProperty("participantObjectIds").EnumerateArray().Select(id => id.GetString()!).Order(StringComparer.Ordinal));
        }
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_CONQUEST_HISTORY_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, scenario));
        driver.Export("initial.json", initial);
        return driver;
    }

    private static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var events = driver.Events().Select(e => e with { Event = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(e.Event, Json), Json)! }).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("commands.json", driver.Commands()); driver.Export("events.json", events); driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = events.Length, replay,
            engineAssembly = typeof(CoreRuleEngine).Assembly.Location, engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Fresh explicit combat fixtures; actual serialized intents, per-command recovery, duplicate intents and JSON journal replay. No ordinary opening, SQL/API or whole-card certification." });
    }
}
