using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class MovementDiscardTests
{
    internal const string Merchant = "OGN·185/298";
    internal static MatchState Position(int handCount = 3, string origin = "BATTLEFIELD")
    {
        var s = MovementSimpleTriggerTests.Position(Merchant, origin);
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects);
        var hand = Enumerable.Range(1, handCount).Select(i => "H" + i).ToArray();
        foreach (var id in hand) cards[id] = new(id, cardNo: "OGN·004/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
        return s with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { Hand = hand } } };
    }

    [Theory]
    [InlineData("BASE", "BATTLEFIELD:BF2")]
    [InlineData("BATTLEFIELD", "BATTLEFIELD:BF2")]
    [InlineData("BATTLEFIELD", "BASE:P2")]
    public async Task EveryRouteWaitsForResponseThenChosenDiscardBeforeDraw(string origin, string destination)
    {
        var moved = await MovementObserverAuditTests.Move(Position(origin: origin), destination);
        Assert.Single(moved.State.StackItems); Assert.Null(moved.State.PendingHandChoice);
        Assert.Equal(new[] { "H1", "H2", "H3" }, moved.State.PlayerZones["P2"].Hand);
        var opened = await Top(moved.State); Restore(opened.State);
        var choice = Assert.IsType<PendingHandChoiceState>(opened.State.PendingHandChoice);
        Assert.Equal("P2", choice.PlayerId); Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { "H1", "H2", "H3" }, choice.LegalObjectIds);
        Assert.DoesNotContain(opened.Events, e => e.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
        Assert.DoesNotContain(opened.Prompts["P1"].Candidates ?? [], c => c.Action == CommandTypes.ChooseHandCards);
        var done = await Act(opened.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]));
        Assert.Equal(new[] { "H1", "H3", "DRAW-TOP" }, done.State.PlayerZones["P2"].Hand);
        Assert.Contains("H2", done.State.PlayerZones["P2"].Graveyard);
        Assert.True(done.Events.ToList().FindIndex(e => e.Kind == "CARD_DISCARDED") < done.Events.ToList().FindIndex(e => e.Kind == "CARD_DRAWN"));
        Assert.Equal(1, done.State.DrawLedger.Counts["P2"]); Restore(done.State);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task InsufficientOrSingleHandStillCompletesTheDraw(int handCount)
    {
        var moved = await MovementObserverAuditTests.Move(Position(handCount), "BASE:P2");
        var done = await Top(moved.State);
        if (done.State.PendingHandChoice is { } choice)
            done = await Act(done.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1"]));
        Assert.Equal(new[] { "DRAW-TOP" }, done.State.PlayerZones["P2"].Hand);
        Assert.Equal(handCount, done.State.PlayerZones["P2"].Graveyard.Count(id => id.StartsWith("H")));
        Assert.Null(done.State.PendingHandChoice); Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturedControllerKeepsChoiceWhenSourceChangesControlOrDisappears(bool disappears)
    {
        var moved = await MovementObserverAuditTests.Move(Position(), "BASE:P2");
        var s = At(moved.State, "D", "P1", "BASE");
        if (disappears) s = s with {
            CardObjects = s.CardObjects.Where(p => p.Key != "D").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = s.PlayerZones["P1"].Base.Where(id => id != "D").ToArray() } },
            ObjectLocations = s.ObjectLocations.Where(p => p.Key != "D").ToDictionary() };
        Restore(s);
        var opened = await Top(s); Restore(opened.State);
        var choice = opened.State.PendingHandChoice!; Assert.Equal("P2", choice.PlayerId);
        var done = await Act(opened.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H3"]));
        Assert.Contains("H3", done.State.PlayerZones["P2"].Graveyard); Assert.Contains("DRAW-TOP", done.State.PlayerZones["P2"].Hand); Restore(done.State);
    }

    [Fact]
    public async Task OpponentIllegalDuplicateAndStaleChoicesAreRejectedWithoutMutation()
    {
        var opened = await Top((await MovementObserverAuditTests.Move(Position(), "BASE:P2")).State);
        var s = opened.State; var choice = s.PendingHandChoice!;
        foreach (var (player, cmd) in new[] {
            ("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"])),
            ("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW-TOP"])),
            ("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2", "H2"])),
            ("P2", new ChooseHandCardsCommand("stale", choice.ChoiceWindow, ["H2"])) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(s, new("invalid", player, cmd.CmdType), cmd, default);
            Assert.False(rejected.Accepted); Assert.Equal(JsonSerializer.Serialize(s), JsonSerializer.Serialize(rejected.State));
        }
    }

    [Fact]
    public async Task ForgedContinuationCannotChangeSkillControllerCountsOrKind()
    {
        var opened = await Top((await MovementObserverAuditTests.Move(Position(), "BASE:P2")).State);
        var s = opened.State; var p = s.PendingHandChoice!;
        foreach (var forged in new[] { p with { DrawCount = 8 }, p with { PlayerId = "P1" },
            p with { EffectKind = "OTHER" }, p with { Reason = "OTHER" }, p with { MovementObserver = null }, p with { MovementObserver = null, EffectKind = "OTHER" }, p with { RequiredCount = 2, MaxCount = 2 } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { PendingHandChoice = forged }), e => e.Contains("movement discard"));
    }
    [Fact]
    public async Task ActualDestructionResponseDoesNotCancelTheEarnedSkill()
    {
        var setup = Position();
        setup = setup with { CardObjects = new Dictionary<string,CardObjectState>(setup.CardObjects) { ["D"] = setup.CardObjects["D"] with { Damage = 1 } } };
        var moved = await MovementObserverAuditTests.Move(setup);
        var s = moved.State with {
            CardObjects = new Dictionary<string, CardObjectState>(moved.State.CardObjects) { ["KILL"] = new("KILL", cardNo: "OGN·133/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(moved.State.PlayerZones) { ["P1"] = moved.State.PlayerZones["P1"] with { Hand = [..moved.State.PlayerZones["P1"].Hand, "KILL"] } },
            RunePools = new Dictionary<string, RunePool>(moved.State.RunePools) { ["P1"] = new(20,20) } };
        if (s.PriorityPlayerId != "P1") s = (await Act(s, s.PriorityPlayerId!, new PassPriorityCommand())).State;
        var cast = await Act(s, "P1", new PlayCardCommand("KILL", "OGN·133/298", []));
        var killed = await Top(cast.State); Assert.Contains("D", killed.State.PlayerZones["P1"].Graveyard);
        var opened = await Top(killed.State); var choice = opened.State.PendingHandChoice!;
        Restore(opened.State);
        var done = await Act(opened.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]));
        Assert.Contains("DRAW-TOP", done.State.PlayerZones["P2"].Hand); Restore(done.State);
    }

    [Fact]
    public async Task DiscardThenBurnoutCanRecycleTheJustDiscardedCard()
    {
        var s = Position(1); s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { MainDeck = [], Graveyard = [] } } };
        var opened = await Top((await MovementObserverAuditTests.Move(s, "BASE:P2")).State);
        var choice = opened.State.PendingHandChoice!;
        var done = await Act(opened.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1"]));
        Assert.Equal(new[] { "H1" }, done.State.PlayerZones["P2"].Hand);
        Assert.Empty(done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(s.PlayerScores.GetValueOrDefault("P1") + 1, done.State.PlayerScores["P1"]);
        Assert.Single(done.Events, e => e.Kind == "BURNOUT_APPLIED"); Restore(done.State);
    }

    [Fact]
    public async Task ChoosingDiscardFeedsSecondDrawObserverOnlyAfterTheDraw()
    {
        var s = Position(); s = s with {
            DrawLedger = new(s.TurnNumber, new Dictionary<string,int> { ["P2"] = 1 }),
            PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = [..s.PlayerZones["P2"].Base, "G"] } },
            CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["G"] = new("G", cardNo:"UNL-074/219", ownerId:"P2", controllerId:"P2", tags:[CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["G"] = new("P2","BASE") } };
        var opened = await Top((await MovementObserverAuditTests.Move(s, "BASE:P2")).State);
        Assert.Null(opened.State.PendingCardChoice);
        var choice = opened.State.PendingHandChoice!;
        var chosen = await Act(opened.State,"P2",new ChooseHandCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["H2"]));
        Assert.Equal(2,chosen.State.DrawLedger.Counts["P2"]);
        Assert.Equal("TRIGGER_CONFIRMATION",chosen.State.PendingCardChoice!.ChoiceWindow);
        var target = chosen.State.PendingCardChoice;
        var selected = await Act(chosen.State,"P2",new ChooseCardsCommand(target.ChoiceId,target.ChoiceWindow,["D"]));
        var done = await Top(selected.State); Assert.Equal(4,done.State.CardObjects["D"].Power); Restore(done.State);
    }

    [Fact]
    public async Task StandardMoveAndASecondMoveEachRequireTheirOwnDiscard()
    {
        var s = Position(origin: "BASE");
        s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var moved = await Act(s, "P2", new MoveUnitCommand("D", "BASE", "BATTLEFIELD:BF2", []));
        var opened = await Top(moved.State); var p = opened.State.PendingHandChoice!;
        var first = await Act(opened.State,"P2",new ChooseHandCardsCommand(p.ChoiceId,p.ChoiceWindow,["H2"]));
        // A ready reset is a fixture checkpoint; the second movement itself uses the real command.
        var ready = first.State with { CardObjects = new Dictionary<string,CardObjectState>(first.State.CardObjects) { ["D"] = first.State.CardObjects["D"] with { IsExhausted = false } } };
        var second = await Act(ready,"P2",new MoveUnitCommand("D","BATTLEFIELD","BASE",[]));
        opened = await Top(second.State); p = opened.State.PendingHandChoice!;
        Assert.Contains("DRAW-TOP",p.LegalObjectIds);
        var done = await Act(opened.State,"P2",new ChooseHandCardsCommand(p.ChoiceId,p.ChoiceWindow,["DRAW-TOP"]));
        Assert.Equal(new[] {"H1","H3","DRAW-NEXT"},done.State.PlayerZones["P2"].Hand);
        Assert.Contains("H2",done.State.PlayerZones["P2"].Graveyard); Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2,done.State.DrawLedger.Counts["P2"]); Restore(done.State);
    }

    [Fact]
    public async Task PendingDiscardDoesNotAllowOtherActionsOrExposePrivateCards()
    {
        var opened = await Top((await MovementObserverAuditTests.Move(Position(),"BASE:P2")).State);
        var s = opened.State;
        foreach (GameCommand cmd in new GameCommand[] { new PassPriorityCommand(),new EndTurnCommand(),new MoveUnitCommand("D","BASE","BATTLEFIELD:BF2",[]) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(s,new("invalid","P2",cmd.CmdType),cmd,default);
            Assert.False(rejected.Accepted);
            Assert.Equal(JsonSerializer.Serialize(s),JsonSerializer.Serialize(rejected.State));
        }
        bool Contains(JsonElement e,string value) => e.ValueKind switch {
            JsonValueKind.String => e.GetString()==value,
            JsonValueKind.Object => e.EnumerateObject().Any(p=>Contains(p.Value,value)),
            JsonValueKind.Array => e.EnumerateArray().Any(x=>Contains(x,value)),
            _=>false };
        foreach(var id in new[]{"H1","H2","H3","DRAW-TOP","DRAW-NEXT"})
        {
            Assert.False(Contains(JsonSerializer.SerializeToElement(opened.Snapshots["P1"]),id));
            Assert.False(Contains(JsonSerializer.SerializeToElement(opened.Prompts["P1"]),id));
        }
        Assert.True(Contains(JsonSerializer.SerializeToElement(opened.Prompts["P2"]),"H2"));
    }

}
