using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Official CN 383.2.a.1 (conditions on capture), 383.4 (independent skill),
// 431.2.a/431.3 (ready runes, as many as available), and each physical printing.
public sealed class RadUnitHoldTests
{
    private const string Ksante = "RAD·086/167", Evelynn = "RAD·109/167";
    public static IEnumerable<object[]> Prints() => new object[][] {
        [Ksante, 39585L, false], ["RAD·086a/167", 39586L, false],
        ["RAD·178*/167", 39695L, false], ["RAD·178/167", 39696L, false],
        [Evelynn, 39611L, true], ["RAD·109a/167", 39612L, true], ["RAD·SP3/005", 39722L, true]
    };
    public static IEnumerable<object[]> Payments() => Prints().SelectMany(p =>
        new[] { false, true }.Where(haste => !(bool)p[2] || !haste)
            .Select(haste => new object[] { p[0], p[2], haste }));
    public static IEnumerable<object[]> InvalidPlays() => Prints().SelectMany(p =>
        new[] { "mana", "power", "targets", "turn" }.Where(b => b != "power" || (bool)p[2])
            .Select(b => new object[] { p[0], b }));

    [Theory]
    [MemberData(nameof(Prints))]
    public async Task EveryOfficialPrintingHasItsOwnBehaviorAndParsedHold(string no, long id, bool score)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(score ? "掌握全场" : "骄勇无敌", card.SubTitle);
        Assert.Equal(score ? 4 : 5, card.Energy); Assert.Equal(4, card.Power);
        Assert.Equal(score ? 1 : 0, card.ReturnEnergy.GetValueOrDefault());
        Assert.Equal(new[] { score ? "purple" : "orange" }, card.CardColorList);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Timing == TriggerTimings.BattlefieldHeld);
        Assert.Equal(score ? TriggerKinds.UnitBattlefieldHeldScore : TriggerKinds.UnitBattlefieldHeldCallRune, trigger.Kind);
        Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(score ? true : (bool?)null, trigger.RequiresControlledFaceDownCardHere);
        Assert.Equal(score ? 1 : (int?)null, trigger.ScoreAmount);
        Assert.Equal(score ? (int?)null : 1, trigger.RuneCallCount);
    }

    [Theory]
    [MemberData(nameof(Payments))]
    public async Task EveryPrintingPaysItsActualCostAndOptionalHaste(string no, bool score, bool haste)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : [], Destination: "BASE"));
        Assert.Equal(20 - (score ? 4 : 5) - (haste ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(20 - (score || haste ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(score ? "缴械" : "坚守2", game.State.CardObjects["UNIT"].Tags);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task InvalidPaymentTargetsAndTimingRejectWithoutChanges(string no, string branch)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (branch is "mana" or "power") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = branch == "mana" ? new(0, 20) : new(20, 0) } };
        if (branch == "turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var cmd = new PlayCardCommand("UNIT", no, branch == "targets" ? ["ALLY"] : [], Destination: "BASE");
        await Reject(state, cmd);
    }

    [Theory]
    [InlineData("mana")] [InlineData("power")] [InlineData("wrong-color")]
    public async Task HasteRequiresItsAdditionalManaAndOrangePower(string branch)
    {
        var state = VenUnitEntryBasicTests.Position(Ksante);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "mana" ? new(5, 20) : branch == "power" ? new(20, 0)
                : new(20, 0, powerByTrait: new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        await Reject(state, new PlayCardCommand("UNIT", Ksante, [], OptionalCosts: [HasteOptionalCostNames.HasteReady], Destination: "BASE"));
    }

    [Theory]
    [MemberData(nameof(Prints))]
    public async Task EveryPrintingResolvesItsHoldAfterResponsesAndBeforeNormalChannel(string no, long id, bool score)
    {
        _ = id;
        var game = new Driver(Position(no, hidden: score));
        await game.Act("P2", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(score ? "UNIT_HIDDEN_HERE_SCORE" : "UNIT_CHANNEL", item.HeldContext!.Kind);
        Assert.Equal(no, item.CardNo); Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(score ? 2 : 1, game.State.PlayerScores["P1"]);
        Assert.Equal(score ? 1 : 0, game.State.PlayerZones["P1"].RuneDeck.Count);
        if (!score) {
            Assert.False(game.State.CardObjects["R1"].IsExhausted);
            var events = game.Events().Select(e => e.Event).ToArray();
            var heldCall = Array.FindIndex(events, e => e.Kind == "RUNES_CALLED");
            var channel = Array.FindIndex(events, e => e.Kind == "RUNES_CALLED" && !e.Payload.ContainsKey("sourceObjectId"));
            Assert.True(heldCall >= 0); Assert.True(channel > heldCall);
        }
        Assert.Equal(MatchPhases.Main, game.State.Phase); await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("face-up")] [InlineData("enemy")]
    [InlineData("other-field")]
    public async Task EvelynnNeedsAControlledFaceDownCardAtHerOwnBattlefield(string branch)
    {
        var state = Position(Evelynn, hidden: branch != "missing");
        if (branch == "face-up") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PRIVATE-STANDBY"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRIVATE-STANDBY", "OGN·219/298", "P1") } };
        if (branch == "enemy") state = LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PRIVATE-STANDBY"] = state.CardObjects["PRIVATE-STANDBY"] with { OwnerId = "P2", ControllerId = "P2" } } }, "PRIVATE-STANDBY", "P2", "BATTLEFIELD", "F");
        if (branch == "other-field") state = LocalDestructionRecallTests.At(state, "PRIVATE-STANDBY", "P1", "BATTLEFIELD", "OTHER");
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(Holds(game.State), h => h.Kind == "UNIT_HIDDEN_HERE_SCORE");
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task RuneCallUsesAllAvailableRunesWithoutAnEmptyDeckPenalty(int available)
    {
        var state = Position(Ksante);
        foreach (var rune in state.PlayerZones["P1"].RuneDeck.Skip(available).ToArray())
            state = LocalDestructionRecallTests.At(state, rune, "P1", "BASE");
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand()); await game.Top();
        var call = Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED" && e.Event.Payload.ContainsKey("sourceObjectId")).Event;
        Assert.Equal(available, Convert.ToInt32(call.Payload["count"]));
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(Ksante)] [InlineData(Evelynn)]
    public async Task OpponentsTurnCannotTriggerTheseUnitsHold(string no)
    {
        var state = Position(no, hidden: true) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        var game = new Driver(state); await game.Act("P1", new EndTurnCommand());
        Assert.Empty(Holds(game.State)); Assert.Equal(0, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(Ksante)] [InlineData(Evelynn)]
    public async Task SourceReturnCannotCancelTheCapturedSkillOrExposeItsPrivateObjectId(string no)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no, hidden: true), "RETURN", "OGN·104/298", "P1");
        state = AddResponseRune(state);
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("HIDDEN", Assert.IsType<Dictionary<string, object?>>(Assert.Single(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack))["sourceObjectId"]);
        var resultCount = game.Events().Length;
        await Drain(game);
        Assert.DoesNotContain(game.Events().Skip(resultCount), e => e.Event.Payload.TryGetValue("sourceObjectId", out var source) && Equals(source, "HOST"));
        Assert.Equal(no == Evelynn ? 2 : 1, game.State.PlayerScores["P1"]);
        await game.Replay();
    }

    [Fact]
    public async Task RemovingTheFaceDownCardDuringResponsesDoesNotRecheckTheCapturedCondition()
    {
        var game = new Driver(Position(Evelynn, hidden: true)); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new RevealCardCommand("PRIVATE-STANDBY", "OGN·057/298", ["HOST"],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        await game.Top(); Assert.Equal("GRAVEYARD", game.State.ObjectLocations["PRIVATE-STANDBY"].Zone);
        await Drain(game); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(Ksante, false)] [InlineData(Ksante, true)]
    [InlineData(Evelynn, false)] [InlineData(Evelynn, true)]
    public async Task CopiedTextAndBlueSentinelCreateIndependentHoldSkills(string no, bool blue)
    {
        var state = Position(no, hidden: true) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        state = AttachedRuleTextTests.AddSong(state, "SONG");
        if (blue) state = OfficialHoldSequenceTests.AddUnit(state, "BLUE", "UNL-087/219", "F", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var held = Holds(game.State).ToArray(); Assert.Equal(blue ? 4 : 2, held.Length);
        Assert.Equal(blue ? 2 : 1, held.Count(h => h.Origin is not null));
        await Drain(game);
        if (no == Evelynn) Assert.Equal(blue ? 5 : 3, game.State.PlayerScores["P1"]);
        else Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        await game.Replay();
    }

    [Fact]
    public async Task EvelynnCanWinWithTheExtraPointAfterNormalHold()
    {
        var state = Position(Evelynn, hidden: true);
        state = state with { PlayerScores = new Dictionary<string, int>(state.PlayerScores) { ["P1"] = 6 } };
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(7, game.State.PlayerScores["P1"]); Assert.Null(game.State.WinnerPlayerId);
        await game.Top(); Assert.Equal(8, game.State.PlayerScores["P1"]); Assert.Equal("P1", game.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, game.State.Status); await game.Replay();
    }

    [Theory]
    [InlineData(Ksante)] [InlineData(Evelynn)]
    public async Task AnEquipmentCopiedTokenKeepsItsHoldWhenAResponseMakesItCeaseToExist(string no)
    {
        var state = LocalDestructionRecallTests.At(Position(no, hidden: true), "HOST", "P1", "BASE")
            with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.SandSoldierTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TOKEN"] = factory.CreateObject("TOKEN", "P1", "P1"),
            ["COPY-GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("COPY-GEAR", "VEN·137", "P1") } };
        state = LocalDestructionRecallTests.At(state, "TOKEN", "P1", "BATTLEFIELD", "F");
        state = LocalDestructionRecallTests.At(state, "COPY-GEAR", "P1", "BASE");
        state = AddResponseRune(AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1"));
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("COPY-GEAR", "TOKEN", ["ASSEMBLE_YELLOW"])); await game.Top();
        var choice = game.State.PendingRuleChoice!;
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:HOST"]));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Single(Holds(game.State));
        await game.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["TOKEN"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("TOKEN"));
        var held = Assert.Single(game.State.StackItems).HeldContext!;
        Assert.True(held.UnitSource!.Ceased); Assert.NotNull(held.Origin);
        Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, held.UnitSource.TokenFactoryCardNo);
        var invalid = game.State with { StackItems = [game.State.StackItems[0] with {
            HeldContext = held with { UnitSource = held.UnitSource with { Ceased = false } } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(invalid));
        await Drain(game); Assert.Equal(no == Evelynn ? 2 : 1, game.State.PlayerScores["P1"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("heldContext")] [InlineData("unitSource")] [InlineData("sourceObjectId")]
    public async Task PublicStackCannotAcquirePrivateHoldContextOrPrivateIdentity(string key)
    {
        var state = AddResponseRune(AttackDamageDistributionTests.Spell(Position(Evelynn, hidden: true), "RETURN", "OGN·104/298", "P1"));
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        state = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, state.Tick, 0, p.Value, state.Tick, 0, prompts[p.Key]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in new[] { snapshots["P2"], spectator.SpectatorSnapshot }) {
            var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal("HIDDEN", row["sourceObjectId"]);
            Assert.DoesNotContain("HOST", JsonSerializer.Serialize(snapshot.Stack));
            var oldValue = row.GetValueOrDefault(key);
            row[key] = key == "sourceObjectId" ? "HOST" : key == "unitSource"
                ? game.State.StackItems[0].HeldContext!.UnitSource : game.State.StackItems[0].HeldContext;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            if (oldValue is null) row.Remove(key); else row[key] = oldValue;
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }

    [Theory]
    [InlineData("amount")] [InlineData("card")] [InlineData("kind")] [InlineData("generation")]
    [InlineData("instance")] [InlineData("capture")] [InlineData("face")]
    [InlineData("factory")] [InlineData("ceased")] [InlineData("source")] [InlineData("missing")]
    public async Task ColdRecoveryRejectsForgedCapturedUnitHold(string mutation)
    {
        var game = new Driver(Position(Ksante)); await game.Act("P2", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems); var held = item.HeldContext!;
        var changed = mutation switch {
            "amount" => held with { Amount = 2 }, "card" => held with { CardNo = Evelynn },
            "kind" => held with { Kind = "UNIT_HIDDEN_HERE_SCORE" },
            "generation" => held with { SourceGeneration = 1 }, "instance" => held with { RuleTextInstanceId = "PRINTED:OTHER:0" },
            "capture" => held with { UnitSource = held.UnitSource! with { CapturedTick = game.State.Tick + 1 } },
            "face" => held with { UnitSource = held.UnitSource! with { FaceCardNo = Evelynn } },
            "factory" => held with { UnitSource = held.UnitSource! with { TokenFactoryCardNo = "OGN·T01/298" } },
            "ceased" => held with { UnitSource = held.UnitSource! with { Ceased = true } }, _ => held };
        if (mutation == "missing") changed = held with { UnitSource = null };
        var forged = game.State with { StackItems = [item with { HeldContext = changed,
            SourceObjectId = mutation == "source" ? "R1" : item.SourceObjectId }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    internal static MatchState Position(string no, bool hidden = false)
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·294/298"), "HOST", no, "F", "P1");
        state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2", Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen, CardPlays = new(5, [], []),
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        // Supply P2's next ordinary draw for the copied-text round trip.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-DECK"] = new("P2-DECK", cardNo: "OGN·219/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) } };
        state = LocalDestructionRecallTests.At(state, "P2-DECK", "P2", "MAIN_DECK");
        if (hidden) {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PRIVATE-STANDBY"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRIVATE-STANDBY", "OGN·057/298", "P1") with { IsFaceDown = true } } };
            state = LocalDestructionRecallTests.At(state, "PRIVATE-STANDBY", "P1", "BATTLEFIELD", "F");
        }
        return state;
    }

    private static IEnumerable<HeldTriggerContext> Holds(MatchState state)
        => state.StackItems.Select(s => s.HeldContext).Concat(state.TriggerQueue.Select(t => t.HeldContext))
            .Where(h => h?.Kind is "UNIT_CHANNEL" or "UNIT_HIDDEN_HERE_SCORE").Select(h => h!);
    private static MatchState AddResponseRune(MatchState state)
        => LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RESPONSE-RUNE"] = new("RESPONSE-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]) } }, "RESPONSE-RUNE", "P1", "BASE");
    private static async Task Drain(Driver game)
    {
        for (var step = 0; step < 40 && game.State.Status != MatchStatuses.Finished
            && (game.State.TurnStartStep is not null || game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0); step++) {
            var order = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p =>
                p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true);
            if (order is not null) await game.Act(order.PlayerId, new OrderTriggersCommand(OrderedTriggerIds:
                game.State.TriggerQueue.Where(t => t.ControllerId == order.PlayerId).Select(t => t.TriggerId).ToArray()));
            else await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.True(game.State.Status == MatchStatuses.Finished || game.State.TurnStartStep is null && game.State.StackItems.Count == 0);
    }
    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rad-hold-reject", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
