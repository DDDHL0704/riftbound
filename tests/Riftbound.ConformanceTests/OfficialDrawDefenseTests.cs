using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialDrawDefenseTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task GemCountsEachCardAndChoosesTargetBeforeResponse(int drawn, bool triggers)
    {
        var state = Gem(drawn);
        var play = await Act(state,"P1",new PlayCardCommand("C","OGN·083/298",[]));
        var draw = await Top(play.State); Restore(draw.State);
        Assert.Equal(drawn+2,draw.State.DrawLedger.Counts["P1"]);
        if (!triggers) { Assert.Empty(draw.State.StackItems); return; }
        Assert.Equal("TRIGGER_CONFIRMATION",draw.State.PendingCardChoice!.ChoiceWindow);
        Assert.Null(draw.State.PriorityPlayerId); Assert.Equal(4,draw.State.CardObjects["V"].Power);
        var confirmed = await Choose(draw.State,["V"]); Restore(confirmed.State);
        Assert.Equal(4,confirmed.State.CardObjects["V"].Power); Assert.NotNull(confirmed.State.PriorityPlayerId);
        var done = await Top(confirmed.State); Restore(done.State);
        Assert.Equal(6,done.State.CardObjects["V"].Power);
        Assert.Equal(2,done.State.CardObjects["V"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain("\"D1\"",JsonSerializer.Serialize(draw.Events));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("opponent")]
    [InlineData("not-in-play")]
    public async Task GemRequiresActiveControlledSource(string mode)
    {
        var state=Gem(0);
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["G"]=state.CardObjects["G"] with {
            IsFaceDown=mode=="hidden",ControllerId=mode=="opponent"?"P2":"P1"}}};
        if(mode=="not-in-play")state=state with {PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Base=["V","MALZ"],Hand=["C","G"]}}};
        var play=await Act(state,"P1",new PlayCardCommand("C","OGN·083/298",[]));var done=await Top(play.State);
        Assert.Empty(done.State.StackItems);Assert.Equal(2,done.State.DrawLedger.Counts["P1"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GemRechecksTargetAndKeepsSourceIndependent(bool targetChanged)
    {
        var play=await Act(Gem(0),"P1",new PlayCardCommand("C","OGN·083/298",[]));var draw=await Top(play.State);
        var confirmed=await Choose(draw.State,["V"]);
        var state=confirmed.State with {PlayerZones=new Dictionary<string,PlayerZones>(confirmed.State.PlayerZones){["P1"]=confirmed.State.PlayerZones["P1"] with {Base=["V","MALZ"],Graveyard=confirmed.State.PlayerZones["P1"].Graveyard.Append("G").ToArray()}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(confirmed.State.ObjectLocations){["G"]=new("P1","GRAVEYARD")}};
        if(targetChanged)state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["V"]=state.CardObjects["V"] with {ObjectGeneration=state.CardObjects["V"].ObjectGeneration+1}}};
        var done=await Top(state);Assert.Equal(targetChanged?4:6,done.State.CardObjects["V"].Power);Restore(done.State);
    }

    [Fact]
    public async Task DianaConditionalDrawIsSecondDrawAndQueuesGem()
    {
        var state=OfficialInsightSourceTests.Diana();
        state=state with {DrawLedger=new(state.TurnNumber,new Dictionary<string,int>{["P2"]=1}),
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with {Base=["R","G"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["G"]=new("G",cardNo:"UNL-074/219",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.EquipmentCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["G"]=new("P2","BASE")}};
        var moved=await Act(state,"P1",new MoveUnitCommand("UNIT",Destination:"BATTLEFIELD:HILL",SourceObjectIds:["UNIT"]));
        var paid=await OfficialInsightSourceTests.Pay(moved.State,"PAY");var opened=await Top(paid.State);
        var draw=await Choose(opened.State,["D1"]);Assert.Equal(2,draw.State.DrawLedger.Counts["P2"]);
        Assert.Equal("TRIGGER_CONFIRMATION",draw.State.PendingCardChoice!.ChoiceWindow);Restore(draw.State);
        var target=await Choose(draw.State,["DIANA"]);var done=await Top(target.State);
        Assert.Equal(5,done.State.CardObjects["DIANA"].Power);Restore(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FanChoosesAndPaysDestructionBeforeResponse(bool decline)
    {
        var waiting=await FanBattle(Fan());Assert.Equal("TRIGGER_CONFIRMATION",waiting.State.PendingCardChoice!.ChoiceWindow);
        Restore(waiting.State);Assert.Contains("D",waiting.State.PlayerZones["P2"].Battlefields);
        var confirmed=await Choose(waiting.State,decline?[]:["A"]);Restore(confirmed.State);
        if(decline){Assert.Contains("D",confirmed.State.PlayerZones["P2"].Battlefields);Assert.Empty(confirmed.State.StackItems);return;}
        Assert.Contains("D",confirmed.State.PlayerZones["P2"].Graveyard);Assert.Contains("A",confirmed.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(confirmed.Events,e=>e.Kind=="DAMAGE_APPLIED");
        var done=await Top(confirmed.State);Assert.Contains("A",done.State.PlayerZones["P1"].Base);
        Assert.True(done.State.CardObjects["A"].IsExhausted);Restore(done.State);
    }

    [Fact]
    public async Task FanWardRequiresGenericPowerAndRejectsBeforeDestroyingSource()
    {
        var state=Fan();state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["A"]=state.CardObjects["A"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var waiting=await FanBattle(state);var choice=waiting.State.PendingCardChoice!;
        var rejected=await new CoreRuleEngine().ResolveAsync(waiting.State,new("bad","P2",CommandTypes.ChooseCards),new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["A"]),default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(waiting.State),MatchStateHasher.Hash(rejected.State));
        var funded=waiting.State with {RunePools=new Dictionary<string,RunePool>(waiting.State.RunePools){["P2"]=new(0,1)}};
        var confirmed=await Choose(funded,["A"]);Assert.Equal(0,confirmed.State.RunePools["P2"].TotalPower);
        Assert.Contains("D",confirmed.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task DianaConfirmationAndResolutionPrecedeFanDefense()
    {
        var state=Fan();state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["A"]=state.CardObjects["A"] with {CardNo="UNL-079/219"}},
            RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(1,0)}};
        var move=await Act(state,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        Assert.NotNull(move.State.PendingPayment);Assert.DoesNotContain(move.State.StackItems,i=>i.FieldContext?.Kind=="DEFEND");
        var paid=await OfficialInsightSourceTests.Pay(move.State,"PAY");var resolved=await Top(paid.State);
        var one=await Act(resolved.State,"P1",new PassFocusCommand());var two=await Act(one.State,"P2",new PassFocusCommand());
        var battle=await Act(two.State,"P1",new DeclareBattleCommand("BF",["A"],["D"],["COMBAT_ASSIGNMENT"]));
        Assert.Equal("DEFEND",battle.State.StackItems.Single().FieldContext!.Kind);Assert.NotNull(battle.State.PendingCardChoice);
    }

    [Theory]
    [InlineData("UNL-032/219", true)]
    [InlineData("OGN·183/298", false)]
    public async Task SelectedDeckCardOnlyCountsWhenOfficialTextSaysDraw(string cardNo, bool counts)
    {
        var state = Gem(1);
        state = state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
            ["C"] = state.CardObjects["C"] with { CardNo = cardNo },
            ["D1"] = state.CardObjects["D1"] with { CardNo = "SFD·125/221", Tags = [CardObjectTags.UnitCard] } } };
        var played = await Act(state, "P1", new PlayCardCommand("C", cardNo, []));
        var pending = await Top(played.State);
        var drawn = await Choose(pending.State, ["D1"]);
        Assert.Equal(counts ? 2 : 1, drawn.State.DrawLedger.Counts["P1"]);
        Assert.Equal(counts, drawn.State.PendingCardChoice?.ChoiceWindow == "TRIGGER_CONFIRMATION");
        Restore(drawn.State);
    }

    [Fact]
    public async Task TurnStartDrawPausesBeforeMainAndResetsOnlyAtNewTurn()
    {
        var state = Gem(1) with { Phase = MatchPhases.TurnStart, TurnStartStep = "DRAW" };
        var started = await Act(state, "P1", new PassPriorityCommand());
        Assert.Equal(2, started.State.DrawLedger.Counts["P1"]);
        Assert.Equal("MAIN", started.State.TurnStartStep);
        Assert.NotNull(started.State.PendingCardChoice);
        var selected = await Choose(started.State, ["V"]);
        var finished = await Top(selected.State);
        Assert.Equal(MatchPhases.Main, finished.State.Phase);
        Assert.Null(finished.State.TurnStartStep);
        var later = Gem(9) with { TurnNumber = state.TurnNumber + 1 };
        var play = await Act(later, "P1", new PlayCardCommand("C", "OGN·083/298", []));
        var draw = await Top(play.State);
        Assert.Equal(2, draw.State.DrawLedger.Counts["P1"]);
        Assert.NotNull(draw.State.PendingCardChoice);
    }

    [Fact]
    public async Task MultipleGemsConfirmInChosenOrderBeforeAnyoneCanRespond()
    {
        var state = Gem(0);
        state = state with { PlayerZones = new Dictionary<string,PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append("G2").ToArray() } },
            CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) { ["G2"] = state.CardObjects["G"] with { ObjectId = "G2" } } };
        var play = await Act(state,"P1",new PlayCardCommand("C","OGN·083/298",[]));
        var draw = await Top(play.State);
        Assert.Equal(2, draw.State.TriggerQueue.Count);
        var order = draw.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray();
        var ordered = await Act(draw.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        Assert.Equal(ordered.State.StackItems[0].SourceObjectId, ordered.State.PendingCardChoice!.SourceObjectId);
        var first = await Choose(ordered.State, ["V"]);
        Assert.Null(first.State.PriorityPlayerId);
        Assert.Equal(ordered.State.StackItems[1].SourceObjectId, first.State.PendingCardChoice!.SourceObjectId);
        var second = await Choose(first.State, ["V"]);
        Restore(second.State);
        var resolved = await Top(second.State); resolved = await Top(resolved.State);
        Assert.Equal(8, resolved.State.CardObjects["V"].Power);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FanResponseThenBattleCompletesWithoutRetriggerOrDeadlock(bool decline)
    {
        var waiting = await FanBattle(Fan());
        var confirmed = await Choose(waiting.State, decline ? [] : ["A"]);
        var response = decline ? confirmed : await Top(confirmed.State);
        var battle = response.State.PriorityPlayerId is null ? response : await Top(response.State);
        Assert.Null(battle.State.PendingCardChoice);
        Assert.DoesNotContain(battle.State.StackItems, i=>i.FieldContext?.Kind == "DEFEND");
        Assert.False(battle.State.BattleState.IsActive);
        if (!decline) Assert.Contains("A", battle.State.PlayerZones["P1"].Base);
        Restore(battle.State);
    }

    [Fact]
    public async Task InvalidConfirmationAndDrawLedgerAreRejectedByRecovery()
    {
        var waiting = await FanBattle(Fan());
        var forged = waiting.State with { PendingCardChoice = waiting.State.PendingCardChoice! with { ResolvingStackItemId = "missing" } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e=>e.Contains("trigger confirmation"));
        forged = waiting.State with { DrawLedger = new(waiting.State.TurnNumber, new Dictionary<string,int> { ["P2"] = -1 }) };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e=>e.Contains("draw ledger"));
        var choice = waiting.State.PendingCardChoice!;
        var wrongPlayer = await new CoreRuleEngine().ResolveAsync(waiting.State, new("forged", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["A"]), default);
        Assert.False(wrongPlayer.Accepted);
        Assert.Equal(MatchStateHasher.Hash(waiting.State), MatchStateHasher.Hash(wrongPlayer.State));
    }

    internal static MatchState Gem(int count)
    {
        var s=OfficialInsightTriggerTests.Visionary();return s with {DrawLedger=new(s.TurnNumber,new Dictionary<string,int>{["P1"]=count}),
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Base=["MALZ","V","G"],Hand=["C"]}},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["G"]=new("G",cardNo:"UNL-074/219",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]),
                ["C"]=new("C",cardNo:"OGN·083/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["G"]=new("P1","BASE")}};
    }
    internal static MatchState Fan(){var s=DeathAndDuelLifecycleTests.State();return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {CardNo="SFD·128/221",Power=2}}};}
    internal static async Task<ResolutionResult> FanBattle(MatchState s)
    {var m=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));var a=await Act(m.State,"P1",new PassFocusCommand());var b=await Act(a.State,"P2",new PassFocusCommand());return await Act(b.State,"P1",new DeclareBattleCommand("BF",["A"],["D"],["COMBAT_ASSIGNMENT"]));}
    internal static Task<ResolutionResult> Act(MatchState s,string p,GameCommand c)=>OfficialInsightTriggerTests.Act(s,p,c);
    internal static Task<ResolutionResult> Top(MatchState s)=>OfficialInsightTriggerTests.ResolveTop(s);
    internal static Task<ResolutionResult> Choose(MatchState s,string[] ids)=>OfficialInsightTriggerTests.Choose(s,ids);
    internal static MatchState Restore(MatchState s)=>OfficialInsightTriggerTests.Restore(s);
}
