using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class DuskRoseLabStartTests
{
    internal static MatchState Position()
    {
        var state = OfficialHoldSequenceTests.State("UNL-209/219");
        state = OfficialHoldSequenceTests.AddUnit(state,"A","OGN·001/298","F","P1");
        return OfficialHoldSequenceTests.AddUnit(state,"B","OGN·001/298","F","P1");
    }
    internal static Task<ResolutionResult> Start(MatchState? state = null) => Act(state ?? Position(),"P1",new PassPriorityCommand());
    internal static Task<ResolutionResult> Choose(MatchState state,params string[] ids)
        => Act(state,"P1",new ChooseCardsCommand(state.PendingCardChoice!.ChoiceId,state.PendingCardChoice.ChoiceWindow,ids));
    [Fact]
    public async Task OptionalDestructionWaitsForPlayerRatherThanChoosingFirstUnit()
    {
        var opened = await Start();
        Assert.NotNull(opened.State.PendingCardChoice);
        Assert.Equal(new[]{"A","B"},opened.State.PendingCardChoice!.LegalObjectIds);
        Assert.Empty(opened.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        Assert.Equal("SCORE",opened.State.TurnStartStep); Restore(opened.State);
    }
    [Fact]
    public async Task DeclineKeepsBothUnitsAndDrawsOnlyNormalTurnCard()
    {
        var opened = await Start(); Assert.NotNull(opened.State.PendingCardChoice);
        var declined = await Choose(opened.State);
        Assert.Contains("A",declined.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("B",declined.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(declined.State.PlayerZones["P1"].Graveyard);
        Assert.Single(declined.State.PlayerZones["P1"].Hand); Restore(declined.State);
    }
    [Fact]
    public async Task SelectedUnitPaysBeforeResponsesAndDrawResolvesAfterward()
    {
        var opened = await Start(); Assert.NotNull(opened.State.PendingCardChoice);
        var confirmed = await Choose(opened.State,"B");
        Assert.Contains("B",confirmed.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("A",confirmed.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(confirmed.State.PlayerZones["P1"].Hand);
        Assert.NotNull(confirmed.State.PriorityPlayerId); Restore(confirmed.State);
        var done = await Top(confirmed.State);
        Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count); Restore(done.State);
    }
    [Theory]
    [InlineData("base")] [InlineData("other")] [InlineData("enemy")] [InlineData("hidden")] [InlineData("dirty-controller")]
    public async Task InvalidCostObjectsRejectWithoutPayingOrDrawing(string change)
    {
        var s = Position();
        if (change == "base") s = At(s,"B","P1","BASE");
        if (change == "other") s = At(s,"B","P1","BATTLEFIELD","OTHER");
        if (change == "enemy") s = At(s,"B","P2","BATTLEFIELD","F");
        if (change is "hidden" or "dirty-controller") s = s with {CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["B"] = change == "hidden" ? s.CardObjects["B"] with {IsFaceDown=true,Tags=[CardObjectTags.UnitCard,CardObjectTags.Standby]}
                : s.CardObjects["B"] with {ControllerId="P2"} } };
        var opened = await Start(s); Assert.DoesNotContain("B",opened.State.PendingCardChoice!.LegalObjectIds);
        var choice = opened.State.PendingCardChoice;
        var command = new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]);
        var bad = await new CoreRuleEngine().ResolveAsync(opened.State,new("invalid","P1",command.CmdType),command,default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(bad.State));
    }
    [Fact]
    public async Task CostIsNotATargetAndDoesNotAddWardPayment()
    {
        var s = Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["B"]=s.CardObjects["B"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]} }};
        var opened=await Start(s);var pools=MatchStateHasher.HashValue(opened.State.RunePools);
        var paid=await Choose(opened.State,"B");
        Assert.Null(paid.State.PendingPayment);Assert.Equal(pools,MatchStateHasher.HashValue(paid.State.RunePools));
        Assert.Empty(Assert.Single(paid.State.StackItems).TargetObjectIds);
        Assert.DoesNotContain(paid.Events,e=>e.Kind=="SPELL_TARGET_CHOSEN");Restore(paid.State);
    }
    [Fact]
    public async Task RecalledInsteadOfDestroyedStillPaysTheInstructionCost()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["B"]=s.CardObjects["B"] with {UntilEndOfTurnEffects=["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]} }};
        var paid=await Choose((await Start(s)).State,"B");
        Assert.Contains("B",paid.State.PlayerZones["P1"].Base);Assert.Empty(paid.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);Assert.NotNull(Assert.Single(paid.State.StackItems).TriggerCost?.DestructionTarget);
        Assert.DoesNotContain(paid.Events,e=>e.Kind=="UNIT_DESTROYED");Restore(paid.State);
        var done=await Top(paid.State);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Fact]
    public async Task PaidDestructionQueuesLastBreathBeforeLabDrawAndNormalDraw()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["B"]=s.CardObjects["B"] with {CardNo="OGN·096/298"}}};
        var paid=await Choose((await Start(s)).State,"B");
        Assert.Equal(2,paid.State.StackItems.Count);Assert.Empty(paid.State.PlayerZones["P1"].Hand);Restore(paid.State);
        var death=await Top(paid.State);Assert.Single(death.State.PlayerZones["P1"].Hand);Assert.Single(death.State.StackItems);Restore(death.State);
        var done=await Top(death.State);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Fact]
    public async Task SourceChangeAfterPaymentDoesNotTransferOrCancelCapturedDraw()
    {
        var paid=await Choose((await Start()).State,"B");var s=paid.State;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["F"]=s.CardObjects["F"] with {ControllerId="P2",CardNo="OGN·294/298",ObjectGeneration=s.CardObjects["F"].ObjectGeneration+1} }};
        Restore(s);var done=await Top(s);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Assert.Empty(done.State.PlayerZones["P2"].Hand);Restore(done.State);
    }
    [Fact]
    public async Task OpponentControlledLabDoesNotTriggerOnYourTurn()
    {
        var s=Position();s=At(At(s,"A","P2","BATTLEFIELD","F"),"B","P2","BATTLEFIELD","F");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["F"]=s.CardObjects["F"] with {ControllerId="P2"}}};
        var done=await Start(s);Assert.Empty(done.State.StackItems);Assert.Null(done.State.PendingCardChoice);
        Assert.Contains("A",done.State.PlayerZones["P2"].Battlefields);Assert.Contains("B",done.State.PlayerZones["P2"].Battlefields);
        Assert.Single(done.State.PlayerZones["P1"].Hand);Restore(done.State);
    }
    [Fact]
    public async Task MultipleLabsConfirmTheirCostsBeforeEitherCanResolve()
    {
        var s=OfficialHoldSequenceTests.AddUnit(Position(),"C","OGN·001/298","OTHER","P1");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["OTHER"]=s.CardObjects["OTHER"] with {CardNo="UNL-209/219",ControllerId="P1"}}};
        var opened=await Start(s);Assert.Equal(2,opened.State.TriggerQueue.Count);Restore(opened.State);
        var ordered=await Act(opened.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:opened.State.TriggerQueue.OrderByDescending(t=>t.SourceObjectId).Select(t=>t.TriggerId).ToArray()));
        Assert.Equal("F",ordered.State.PendingCardChoice!.SourceObjectId);
        var first=await Choose(ordered.State,"B");Assert.Equal("OTHER",first.State.PendingCardChoice!.SourceObjectId);
        Assert.Null(first.State.PriorityPlayerId);Assert.Empty(first.State.PlayerZones["P1"].Hand);Restore(first.State);
        var second=await Choose(first.State,"C");Assert.Null(second.State.PendingCardChoice);Assert.Equal(2,second.State.StackItems.Count);Restore(second.State);
        var done=await Top((await Top(second.State)).State);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Theory]
    [InlineData("source")] [InlineData("generation")] [InlineData("unit")] [InlineData("mana")] [InlineData("missing")]
    public async Task RecoveryRejectsMalformedPaidDestructionReceipt(string change)
    {
        var paid=await Choose((await Start()).State,"B");var item=Assert.Single(paid.State.StackItems);var receipt=item.TriggerCost!;
        var changed=change switch {
            "source"=>receipt with {Source=new("A",receipt.Source.Generation)},
            "generation"=>receipt with {DestructionTarget=new("B",-1)},
            "unit"=>receipt with {DestructionTarget=new("F",0)},
            "mana"=>receipt with {Mana=1},_=>null};
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(paid.State with {StackItems=[item with {TriggerCost=changed}]}));
    }
    [Fact]
    public async Task PublicPaidReceiptCannotDifferForEitherPlayerOrSpectator()
    {
        var paid=await Choose((await Start()).State,"B");var s=paid.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,paid.Snapshots[id],s.Tick,0,paid.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        Assert.Empty(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator));
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));var original=payload["triggerCost"];
            payload["triggerCost"]=s.StackItems[0].TriggerCost! with {DestructionTarget=new("A",0)};
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("destruction cost receipt differs"));payload["triggerCost"]=original;
        }
    }

    [Fact]
    public async Task ATokenCanPayAndItsReceiptSurvivesTheTokenCeasingToExist()
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo,out var factory));
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["B"]=factory.CreateObject("B","P1","P1")}};
        var paid=await Choose((await Start(s)).State,"B");
        Assert.False(paid.State.CardObjects.ContainsKey("B"));Assert.DoesNotContain("B",paid.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("B",Assert.Single(paid.State.StackItems).TriggerCost!.DestructionTarget!.ObjectId);Restore(paid.State);
        var done=await Top(paid.State);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Fact]
    public async Task NoLocalUnitDoesNotOpenAnUnpayableChoiceOrDrawExtra()
    {
        var s=At(At(Position(),"A","P1","BASE"),"B","P1","BASE");
        var done=await Start(s);Assert.Null(done.State.PendingCardChoice);Assert.Empty(done.State.StackItems);
        Assert.Single(done.State.PlayerZones["P1"].Hand);Assert.Contains("A",done.State.PlayerZones["P1"].Base);Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MixedFrostAndLabMustFinishCostConfirmationBeforeAnyDamage(bool reverse)
    {
        var s=OfficialHoldSequenceTests.AddUnit(Position(),"X","OGN·001/298","OTHER","P2");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["OTHER"]=s.CardObjects["OTHER"] with {CardNo="UNL-212/219",ControllerId="P1"},["X"]=s.CardObjects["X"] with {Power=8}}};
        var opened=await Start(s);Assert.Equal(2,opened.State.TriggerQueue.Count);
        var ids=opened.State.TriggerQueue.Select(t=>t.TriggerId).ToArray();if(reverse)Array.Reverse(ids);
        var ordered=await Act(opened.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:ids));
        Assert.NotNull(ordered.State.PendingCardChoice);Assert.Null(ordered.State.PriorityPlayerId);Assert.Equal(0,ordered.State.CardObjects["X"].Damage);
        var paid=await Choose(ordered.State,"B");Assert.Equal(0,paid.State.CardObjects["X"].Damage);Assert.Contains("B",paid.State.PlayerZones["P1"].Graveyard);Restore(paid.State);
        var done=await Top((await Top(paid.State)).State);Assert.Equal(1,done.State.CardObjects["X"].Damage);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }

}
