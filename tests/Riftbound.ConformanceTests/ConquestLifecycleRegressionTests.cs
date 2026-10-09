using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConquestLifecycleRegressionTests
{
    [Fact]
    public async Task EmptyZaunConquestWaitsForChosenDiscardBeforeDrawing()
    {
        var result = await Conquer(State());
        var choice = Assert.IsType<PendingHandChoiceState>(result.State.PendingHandChoice);
        var candidate = Assert.Single(result.Prompts["P1"].Candidates!, c => c.Action == CommandTypes.ChooseHandCards);
        Assert.NotNull(candidate.CommandTemplate);
        Assert.Single(candidate.SelectionSteps!);
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_CONQUEST_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
            File.WriteAllText(Path.Combine(evidence, "hand-choice-prompt.json"),
                JsonSerializer.Serialize(result.Prompts["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { "H1", "H2", "H3" }, choice.LegalObjectIds);
        Assert.Equal(3, result.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(result.Events, e => e.Kind is "CARD_DRAWN" or "CARD_DISCARDED");
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        var selected = await Resolve(restored, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]));
        Assert.Null(selected.State.PendingHandChoice);
        Assert.Equal(new[] { "H1", "H3", "DRAW1" }, selected.State.PlayerZones["P1"].Hand);
        Assert.Contains("H2", selected.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, Assert.Single(selected.Events, e => e.Kind == "CARD_DRAWN").Payload["count"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ForcedOrEmptyHandStillDrawsOne(int handCount)
    {
        var result = await Conquer(State(handCount: handCount));
        Assert.Null(result.State.PendingHandChoice);
        Assert.Equal(new[] { "DRAW1" }, result.State.PlayerZones["P1"].Hand);
        Assert.Equal(handCount, result.State.PlayerZones["P1"].Graveyard.Count);
    }

    [Fact]
    public async Task RepeatedControlDoesNotRetriggerConquestAfterScoring()
    {
        var state = State() with { UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1")] };
        var result = await Conquer(state);
        Assert.Equal("P1", result.State.CardObjects["BF"].ControllerId);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Null(result.State.PendingHandChoice);
    }

    [Fact]
    public async Task OpponentsScoreDoesNotConsumeMyConquestAllowance()
    {
        var result = await Conquer(State() with {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P2")] });
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.NotNull(result.State.PendingHandChoice);
    }

    [Theory]
    [InlineData("OGN·039/298", "CARD_DRAWN")]
    [InlineData("SFD·069/221", "EQUIPMENT_TOKEN_CREATED")]
    public async Task EmptyConquestAlsoDispatchesUnitSkills(string cardNo, string expectedEvent)
    {
        var state = State("OGN·296/298");
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["UNIT"] = cards["UNIT"] with { CardNo = cardNo };
        var result = await Conquer(state with { CardObjects = cards });
        Assert.Contains(result.Events, e => e.Kind == expectedEvent);
    }

    [Fact]
    public async Task CombatConquestAlsoWaitsForPlayerSelectedHandCard()
    {
        var state = State();
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { Base = ["RUNE", "RUNE2"], Battlefields = ["UNIT"] };
        zones["P2"] = zones["P2"] with { Battlefields = ["BF", "DEFENDER"] };
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1,
            tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        var result = await Resolve(state with { PlayerZones = zones, CardObjects = cards }, "P1",
            new DeclareBattleCommand("BF", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.NotNull(result.State.PendingHandChoice);
        Assert.Equal(3, result.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(result.Events, e => e.Kind == "CARD_DISCARDED");
    }

    [Fact]
    public async Task HandChoiceRejectsOpponentIllegalAndStaleInputsWithoutChangingState()
    {
        var pending = await Conquer(State());
        var choice = pending.State.PendingHandChoice!;
        foreach (var (player, command) in new[] {
            ("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"])),
            ("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW1"])),
            ("P1", new ChooseHandCardsCommand("expired", choice.ChoiceWindow, ["H2"])),
            ("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2", "H2"])) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(pending.State,
                new PlayerIntent("invalid", player, command.CmdType), command, CancellationToken.None);
            Assert.False(rejected.Accepted);
            Assert.Equal(JsonSerializer.Serialize(pending.State), JsonSerializer.Serialize(rejected.State));
        }
        Assert.DoesNotContain("H2", JsonSerializer.Serialize(pending.Prompts["P2"]));
        Assert.Contains("H2", JsonSerializer.Serialize(pending.Prompts["P1"]));
    }

    [Fact]
    public async Task ConquestCreatedUnitRemainsOnTheConqueredBattlefield()
    {
        var state = State("OGN·296/298");
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["LEGEND"] = new("LEGEND", cardNo: "UNL-199/219", ownerId: "P1", controllerId: "P1");
        cards["OWN-BF"] = new("OWN-BF", cardNo: "OGN·280/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1");
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"], Battlefields = ["OWN-BF"] };
        var result = await Conquer(state with { CardObjects = cards, PlayerZones = zones });
        Assert.Empty(OfficialTokenReplacementTests.Tokens(result.State));
        result = await OfficialTokenReplacementTests.Choose(result.State, "H2");
        result = await OfficialGraveyardRecastTests.Top(result.State);
        var token = Assert.Single(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        var id = Assert.IsType<string>(token.Payload["tokenObjectId"]);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), result.State.ObjectLocations[id]);
    }

    [Theory]
    [InlineData("OGN·289/298", "BATTLEFIELD_CONQUERED_READY_RUNES_AT_END")]
    [InlineData("SFD·212/221", "BATTLEFIELD_CONQUERED_MILL_TOP_TWO")]
    public async Task EmptyConquestDispatchesOtherBattlefieldSkills(string cardNo, string triggerKind)
    {
        var result = await Conquer(State(cardNo));
        Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
            && Equals(e.Payload.GetValueOrDefault("trigger"), triggerKind));
    }

    internal static async Task<ResolutionResult> Conquer(MatchState state)
    {
        var moved = await Resolve(state, "P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        var pass = await Resolve(moved.State, "P1", new PassFocusCommand());
        return await Resolve(pass.State, "P2", new PassFocusCommand());
    }

    private static async Task<ResolutionResult> Resolve(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent(Guid.NewGuid().ToString(), player, command.CmdType), command, CancellationToken.None);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    internal static MatchState State(string battlefieldCardNo = "OGN·298/298", int handCount = 3)
    {
        var hand = Enumerable.Range(1, handCount).Select(i => $"H{i}").ToArray();
        var cards = hand.Concat(new[] { "DRAW1", "DRAW2", "DRAW3" }).ToDictionary(id => id,
            id => new CardObjectState(id, cardNo: "OGN·004/298", tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"));
        cards["BF"] = new("BF", cardNo: battlefieldCardNo, tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2");
        cards["UNIT"] = new("UNIT", cardNo: "SFD·125/221", power: 4, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
        cards["RUNE"] = new("RUNE", cardNo: "OGN·007/298", tags: [CardObjectTags.RuneCard], ownerId: "P1", controllerId: "P1", isExhausted: true);
        cards["RUNE2"] = cards["RUNE"] with { ObjectId = "RUNE2" };
        return new MatchState(roomId: "conquest-regression", tick: 0, turnNumber: 5, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["UNIT", "RUNE", "RUNE2"], Hand = hand, MainDeck = ["DRAW1", "DRAW2", "DRAW3"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF"] } },
            cardObjects: cards, playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty });
    }
}
