using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeathAndDuelLifecycleTests
{
    [Fact]
    public async Task ReactionPermissionCoversEveryRegisteredReactionAndRejectsOrdinaryCardsAndWrongPlayer()
    {
        var response = await OpenBattle(State());
        var reactions = CardBehaviorRegistry.GetAll().Where(b => b.CanPlayDuringPriority
            || CardPermissionKeywordRules.HasSourceKeyword(b, CardPermissionKeywordNames.Reaction)).ToArray();
        Assert.NotEmpty(reactions);
        foreach (var card in reactions)
        {
            Assert.True(CardPermissionKeywordRules.EvaluatePlayTiming(response.State, "P2", card).IsAllowed, card.CardNo);
            Assert.False(CardPermissionKeywordRules.EvaluatePlayTiming(response.State, "P1", card).IsAllowed, card.CardNo);
        }
        foreach (var card in CardBehaviorRegistry.GetAll().Where(b => !b.CanPlayDuringPriority
            && !CardPermissionKeywordRules.HasSourceKeyword(b, CardPermissionKeywordNames.Reaction)))
            Assert.False(CardPermissionKeywordRules.EvaluatePlayTiming(response.State, "P2", card).IsAllowed, card.CardNo);
    }

    [Fact]
    public async Task DefenderReactionPreviewAndSubmissionUseTheSameServerWindow()
    {
        var response = await OpenBattle(State());
        var request = new PlayCostPreviewRequestDto("response-preview", response.Prompts["P2"].PromptId!, response.State.Tick,
            new PlayCardCommand("SPELL", "OGN·095/298", ["A"]));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DEATH_DUEL_EVIDENCE") is { Length: > 0 } root
            && File.Exists(Path.Combine(root, "native-response-preview.json")))
            request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(root, "native-response-preview.json")))!;
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(response.State, "P2", request);
        Assert.True(quote.IsValid, quote.Message);
        Assert.True(quote.CanPay, quote.Message);
        var session = new MatchSession(response.State, engine, NoopMatchJournal.Instance);
        var raw = new Dictionary<string, object?> {
            ["cmdType"] = "PLAY_CARD", ["sourceObjectId"] = "SPELL", ["cardNo"] = "OGN·095/298",
            ["targetObjectIds"] = new[] { "A" }, ["promptId"] = request.PromptId, ["snapshotTick"] = request.SnapshotTick
        };
        var accepted = await session.SubmitAsync("P2", "native-reaction", request.Command, JsonSerializer.SerializeToElement(raw), default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Single(accepted.State.StackItems);
        var duplicate = await session.SubmitAsync("P2", "native-reaction", request.Command, JsonSerializer.SerializeToElement(raw), default);
        Assert.Equal(MatchStateHasher.Hash(accepted.State), MatchStateHasher.Hash(duplicate.State));
        var stale = await session.SubmitAsync("P2", "stale-reaction", request.Command, JsonSerializer.SerializeToElement(raw), default);
        Assert.False(stale.Accepted);
        Assert.Equal(MatchStateHasher.Hash(accepted.State), MatchStateHasher.Hash(stale.State));
    }

    [Theory]
    [InlineData("OGN·096/298")]
    [InlineData("OGN·178/298")]
    [InlineData("OGN·216/298")]
    [InlineData("OGN·239/298")]
    [InlineData("SFD·021/221")]
    [InlineData("SFD·155/221")]
    [InlineData("SFD·167/221")]
    [InlineData("SFD·036/221")]
    public async Task CombatDeathFamiliesRetainAndResolveTheirTriggeredEffect(string cardNo)
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["D"] = cards["D"] with { CardNo = cardNo, Power = 5 };
        cards["A"] = cards["A"] with { Power = 12 };
        var response = await OpenBattle(state with { CardObjects = cards });
        var first = await Act(response.State, "P2", new PassPriorityCommand());
        var death = await Act(first.State, "P1", new PassPriorityCommand());
        Assert.Contains(death.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.Single(death.State.StackItems);
        SaveEvidence("death", death);
        var trigger = death.State.StackItems[0];
        Assert.Equal("D", trigger.SourceObjectId);
        var pass = await Act(death.State, death.State.PriorityPlayerId!, new PassPriorityCommand());
        var resolved = await Act(pass.State, pass.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.DoesNotContain(resolved.State.StackItems, s => s.StackItemId == trigger.StackItemId);
        Assert.Contains(resolved.Events, e => e.Kind == "TRIGGER_RESOLVED");
    }

    [Fact]
    public async Task TwoCombatDeathsRequireOrderingAndEachDrawsExactlyOnce()
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["D2"] = cards["D"] with { ObjectId = "D2" };
        cards["DRAW2"] = cards["DRAW"] with { ObjectId = "DRAW2" };
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P2"] = zones["P2"] with { Battlefields = ["BF", "D", "D2"], MainDeck = ["DRAW", "DRAW2"] };
        var locations = state.ObjectLocations.ToDictionary(x => x.Key, x => x.Value);
        locations["D2"] = new("P2", "BATTLEFIELD", "BF");
        var response = await OpenBattle(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations }, ["D", "D2"]);
        var first = await Act(response.State, "P2", new PassPriorityCommand());
        var death = await Act(first.State, "P1", new PassPriorityCommand());
        Assert.Equal(2, death.State.TriggerQueue.Count);
        Assert.Contains(death.Prompts["P2"].Candidates!, c => c.Action == "ORDER_TRIGGERS" && c.Enabled);
        var ordered = await Act(death.State, "P2", new OrderTriggersCommand(death.State.TriggerQueue.Reverse().Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 4; i++) ordered = await Act(ordered.State, ordered.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("DRAW", ordered.State.PlayerZones["P2"].Hand);
        Assert.Contains("DRAW2", ordered.State.PlayerZones["P2"].Hand);
        Assert.Empty(ordered.State.StackItems);
        Assert.Empty(ordered.State.TriggerQueue);
    }

    [Fact]
    public async Task MultipleDeathsDuringSpellDuelResumeTheSameDuelAfterOrderingAndRecovery()
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["D2"] = cards["D"] with { ObjectId = "D2" };
        cards["DRAW2"] = cards["DRAW"] with { ObjectId = "DRAW2" };
        cards["AOE"] = new("AOE", cardNo: "OGN·133/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { Hand = ["AOE"] };
        zones["P2"] = zones["P2"] with { Battlefields = ["BF", "D", "D2"], MainDeck = ["DRAW", "DRAW2"] };
        var locations = state.ObjectLocations.ToDictionary(x => x.Key, x => x.Value);
        locations["D2"] = new("P2", "BATTLEFIELD", "BF");
        var pools = state.RunePools.ToDictionary(x => x.Key, x => x.Value);
        pools["P1"] = new(20, 20, new Dictionary<string, int> { ["blue"] = 20 });
        state = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        var focus = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var cast = await Act(focus.State, "P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        var pass = await Act(cast.State, cast.State.PriorityPlayerId!, new PassPriorityCommand());
        var deaths = await Act(pass.State, pass.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(2, deaths.State.TriggerQueue.Count);
        Assert.All(deaths.State.TriggerQueue, t => Assert.Equal(TimingStates.SpellDuelOpen, t.TimingContext));
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(deaths.State))!;
        var ordered = await Act(recovered, "P2", new OrderTriggersCommand(recovered.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 4; i++) ordered = await Act(ordered.State, ordered.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.True(ordered.State.SpellDuelState.IsActive);
        Assert.Equal("BF", ordered.State.SpellDuelState.BattlefieldObjectId);
        Assert.NotNull(ordered.State.FocusPlayerId);
        Assert.Contains(ordered.Prompts[ordered.State.FocusPlayerId!].Candidates!, c => c.Action == "PASS_FOCUS" && c.Enabled);
        Assert.Contains("DRAW2", ordered.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CombatDeathKeepsSentinelTriggerAndDrawsOnlyAfterBothPlayersPass()
    {
        var response = await OpenBattle(State());
        SaveEvidence("battle-response", response);
        var first = await Act(response.State, "P2", new PassPriorityCommand());
        var death = await Act(first.State, "P1", new PassPriorityCommand());
        Assert.Contains("D", death.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(death.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.Single(death.State.StackItems);
        Assert.DoesNotContain("DRAW", death.State.PlayerZones["P2"].Hand);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(death.State))!;
        var p1 = await Act(restored, restored.PriorityPlayerId!, new PassPriorityCommand());
        var drawn = await Act(p1.State, p1.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("DRAW", drawn.State.PlayerZones["P2"].Hand);
        Assert.Empty(drawn.State.StackItems);
        Assert.Empty(drawn.State.TriggerQueue);
    }

    [Fact]
    public async Task DefenderCanCastExtortionInEmptyBattleResponseAndCombatWaits()
    {
        var response = await OpenBattle(State());
        var play = Assert.Single(response.Prompts["P2"].Candidates!, c => c.Action == "PLAY_CARD");
        Assert.Contains(play.Sources!, s => s.Id == "SPELL");
        var cast = await Act(response.State, "P2", new PlayCardCommand("SPELL", "OGN·095/298", ["A"], ""));
        Assert.Single(cast.State.StackItems);
        Assert.True(cast.State.BattleState.IsActive);
        Assert.DoesNotContain(cast.Events, e => e.Kind == "DAMAGE_APPLIED");
        var first = await Act(cast.State, cast.State.PriorityPlayerId!, new PassPriorityCommand());
        var resolved = await Act(first.State, first.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("DRAW", resolved.State.PlayerZones["P2"].Hand);
        Assert.Equal(3, resolved.State.CardObjects["A"].Power);
        Assert.True(resolved.State.BattleState.IsActive);
        Assert.NotNull(resolved.State.PriorityPlayerId);
    }

    [Fact]
    public async Task DefenderCanCastExtortionDuringFocusButOpponentCannotStealTheWindow()
    {
        var moved = await Act(State(), "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var focus = await Act(moved.State, "P1", new PassFocusCommand());
        Assert.Equal("P2", focus.State.FocusPlayerId);
        SaveEvidence("focus", focus);
        Assert.Contains(focus.Prompts["P2"].Candidates!, c => c.Action == "PLAY_CARD" && c.Enabled);
        var rejected = await new CoreRuleEngine().ResolveAsync(focus.State, new("wrong", "P1", "PLAY_CARD"),
            new PlayCardCommand("SPELL", "OGN·095/298", ["A"], ""), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(JsonSerializer.Serialize(focus.State), JsonSerializer.Serialize(rejected.State));
        var cast = await Act(focus.State, "P2", new PlayCardCommand("SPELL", "OGN·095/298", ["A"], ""));
        Assert.True(cast.State.SpellDuelState.IsActive);
        Assert.True(cast.State.SpellDuelState.IsClosed);
        SaveEvidence("duel-stack", cast);
    }

    private static void SaveEvidence(string name, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DEATH_DUEL_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        File.WriteAllText(Path.Combine(root, name + "-state.json"), JsonSerializer.Serialize(result.State));
        foreach (var player in new[] { "P1", "P2" })
        {
            File.WriteAllText(Path.Combine(root, name + "-" + player + "-snapshot.json"), JsonSerializer.Serialize(result.Snapshots[player], web));
            File.WriteAllText(Path.Combine(root, name + "-" + player + "-prompt.json"), JsonSerializer.Serialize(result.Prompts[player], web));
        }
    }

    private static async Task<ResolutionResult> OpenBattle(MatchState state, string[]? defenders = null)
    {
        var moved = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var response = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], defenders ?? ["D"], ["COMBAT_ASSIGNMENT"]));
        Assert.Empty(response.State.StackItems);
        Assert.Equal("P2", response.State.PriorityPlayerId);
        return response;
    }

    private static async Task<ResolutionResult> Act(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent(Guid.NewGuid().ToString(), player, command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    internal static MatchState State()
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["A"] = new("A", cardNo: "OGN·012/298", power: 4, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["D"] = new("D", cardNo: "OGN·096/298", power: 1, tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2"),
            ["BF"] = new("BF", cardNo: "OGN·297/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2"),
            ["SPELL"] = new("SPELL", cardNo: "OGN·095/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2"),
            ["DRAW"] = new("DRAW", cardNo: "OGN·012/298", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2") };
        return new MatchState("death-duel", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["A"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D"], Hand = ["SPELL"], MainDeck = ["DRAW"] } },
            cardObjects: cards, objectLocations: new Dictionary<string, ObjectLocationState> {
                ["A"] = new("P1", "BASE"), ["D"] = new("P2", "BATTLEFIELD", "BF"), ["BF"] = new("P2", "BATTLEFIELD", "BF") },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(1, 0) });
    }
}
