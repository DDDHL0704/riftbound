using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class MundoBeginningTests
{
    internal static MatchState Position(int graveCount=5)
    {
        var s=OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·278/298"),"MUNDO","OGN·109/298","F","P1");
        var cards=s.CardObjects.ToDictionary(p=>p.Key,p=>p.Value);cards["MUNDO"]=cards["MUNDO"] with {Power=6};
        var locations=s.ObjectLocations.ToDictionary(p=>p.Key,p=>p.Value);var grave=Enumerable.Range(1,graveCount).Select(n=>"G"+n).ToArray();
        foreach(var id in grave){cards[id]=new(id,cardNo:"OGN·001/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]);locations[id]=new("P1","GRAVEYARD");}
        return s with {CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Graveyard=grave}}};
    }
    internal static int Might(MatchState s)=>CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("MUNDO").GetProperty("effectivePower").GetInt32();
    [Fact]
    public void CurrentMightIncludesCurrentControllerGraveyardRatherThanOnlyEntryCount()
    {
        var s=Position(2);Assert.Equal(8,Might(s));Restore(s);
        s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Graveyard=[]}}};
        Assert.Equal(6,Might(s));
    }
    [Fact]
    public async Task BeginningRecycleWaitsForResponsesThenAsksForThreeCards()
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());Assert.Single(opened.State.StackItems);
        Assert.Null(opened.State.PendingCardChoice);Assert.Equal(5,opened.State.PlayerZones["P1"].Graveyard.Count);Restore(opened.State);
        var choice=await Top(opened.State);Assert.NotNull(choice.State.PendingCardChoice);Assert.Equal(3,choice.State.PendingCardChoice!.RequiredCount);
        Assert.Equal(5,choice.State.PendingCardChoice.LegalObjectIds.Count);Assert.Empty(choice.State.PlayerZones["P1"].Hand);Restore(choice.State);
    }

    internal static async Task<ResolutionResult> Choice(MatchState state, params string[] ids)
    {
        var c=state.PendingCardChoice!;return await Act(state,c.PlayerId,new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,ids));
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)]
    public async Task MandatoryRecycleTakesThreeOrAllAvailableThenContinuesBeforeNormalDraw(int count)
    {
        var s=Position(count);var before=s.PlayerZones["P1"].MainDeck.ToArray();
        var opened=await Act(s,"P1",new PassPriorityCommand());var paused=await Top(opened.State);
        var choice=paused.State.PendingCardChoice!;Assert.Equal(Math.Min(3,count),choice.RequiredCount);Restore(paused.State);
        var selected=choice.LegalObjectIds.TakeLast(choice.RequiredCount).ToArray();var done=await Choice(paused.State,selected);
        Assert.Null(done.State.PendingCardChoice);Assert.Empty(done.State.StackItems);Assert.Equal(MatchPhases.Main,done.State.Phase);
        Assert.Equal(count-selected.Length,done.State.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(selected.Order(),done.State.PlayerZones["P1"].MainDeck.TakeLast(selected.Length).Order());
        Assert.Equal(before[0],Assert.Single(done.State.PlayerZones["P1"].Hand));Assert.Equal(6+count-selected.Length,Might(done.State));
        Assert.Equal(s.RngCursor+(selected.Length>1?1:0),done.State.RngCursor);Restore(done.State);
    }
    [Fact]
    public async Task EmptyGraveyardStillQueuesButNeedsNoChoiceOnResolution()
    {
        var opened=await Act(Position(0),"P1",new PassPriorityCommand());Assert.Single(opened.State.StackItems);
        var done=await Top(opened.State);Assert.Null(done.State.PendingCardChoice);Assert.Equal(MatchPhases.Main,done.State.Phase);Restore(done.State);
    }
    [Theory]
    [InlineData("G1,G2")] [InlineData("G1,G1,G2")] [InlineData("G1,G2,NOT-MINE")] [InlineData("G1,G2,G3,G4")]
    public async Task InvalidSelectionIsAtomic(string raw)
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());var paused=await Top(opened.State);var s=paused.State;var c=s.PendingCardChoice!;
        var rejected=await new CoreRuleEngine().ResolveAsync(s,new("bad","P1",CommandTypes.ChooseCards),new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,raw.Split(',')),default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(rejected.State));Restore(rejected.State);
    }
    [Fact]
    public async Task RecyclingCanMakePreviouslyNonlethalDamageLethalBeforeScoring()
    {
        var s=Position(3);s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["MUNDO"]=s.CardObjects["MUNDO"] with {Damage=7}}};
        Assert.Equal(9,Might(s));var opened=await Act(s,"P1",new PassPriorityCommand());var paused=await Top(opened.State);
        var done=await Choice(paused.State,"G1","G2","G3");Assert.Contains("MUNDO",done.State.PlayerZones["P1"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task DepartedSourceDoesNotCancelItsCapturedControllerRecycle()
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());var s=opened.State;
        s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Battlefields=s.PlayerZones["P1"].Battlefields.Where(x=>x!="MUNDO").ToArray(),Hand=["MUNDO"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["MUNDO"]=new("P1","HAND")},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["MUNDO"]=s.CardObjects["MUNDO"] with {ObjectGeneration=1}}};
        Restore(s);var paused=await Top(s);var done=await Choice(paused.State,"G3","G4","G5");Assert.Equal(2,done.State.PlayerZones["P1"].Graveyard.Count);Restore(done.State);
    }
    [Fact]
    public void GraveyardPowerTracksNewControllerAndDoesNotApplyWhileHidden()
    {
        var s=Position(4);Assert.Equal(10,Might(s));s=At(s,"MUNDO","P2","BASE");
        var power=CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("MUNDO").GetProperty("effectivePower").GetInt32();
        Assert.Equal(6,power);Restore(s);
        s=Position(4);s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["MUNDO"]=s.CardObjects["MUNDO"] with {IsFaceDown=true,Tags=[CardObjectTags.UnitCard,CardObjectTags.Standby]}}};Assert.Equal(6,Might(s));
    }

    [Fact]
    public void GraveyardPowerAddsToBattlefieldAuraAndTemporaryPower()
    {
        var s=Position(2);s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["F"]=s.CardObjects["F"] with {CardNo="OGN·294/298"},
            ["MUNDO"]=s.CardObjects["MUNDO"] with {Power=8,UntilEndOfTurnPowerModifier=2}}};
        Assert.Equal(11,Might(s));Restore(s);
    }
    [Fact]
    public async Task ForgedCapturedContextAndChoiceAreRejectedByCurrentRecovery()
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());var s=opened.State;var item=s.StackItems.Single();
        foreach(var context in new[]{item.BeginningRecycle! with {Count=4},item.BeginningRecycle! with {Completed=true},item.BeginningRecycle! with {CardNo="OGN·001/298"}})
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[item with {BeginningRecycle=context}]}));
        var paused=await Top(s);s=paused.State;var c=s.PendingCardChoice!;
        foreach(var choice in new[]{c with {RequiredCount=2},c with {MaxCount=4},c with {PlayerId="P2"},c with {LegalObjectIds=["G1","G2","G3"]},c with {ResolvingStackItemId="OTHER"}})
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with {PendingCardChoice=choice}));
        Restore(s);
    }
    [Fact]
    public async Task TwoSourcesRecycleIndependentlyUsingRemainingGraveyardAtEachResolution()
    {
        var s=OfficialHoldSequenceTests.AddUnit(Position(),"MUNDO2","OGN·109/298","F","P1");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["MUNDO2"]=s.CardObjects["MUNDO2"] with {Power=6}}};
        var opened=await Act(s,"P1",new PassPriorityCommand());Assert.Equal(2,opened.State.TriggerQueue.Count);
        var ordered=await Act(opened.State,"P1",new OrderTriggersCommand(opened.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));Restore(ordered.State);
        var first=await Top(ordered.State);var done=await Choice(first.State,"G1","G2","G3");Assert.Single(done.State.StackItems);Assert.Null(done.State.PendingCardChoice);Restore(done.State);
        var second=await Top(done.State);Assert.Equal(2,second.State.PendingCardChoice!.RequiredCount);
        done=await Choice(second.State,"G4","G5");Assert.Empty(done.State.PlayerZones["P1"].Graveyard);Assert.Equal(6,Might(done.State));Restore(done.State);
    }
    [Fact]
    public async Task NormalPlayKeepsPrintedBaseAndProjectsCurrentPower()
    {
        var s=Position(2);s=s with {Phase=MatchPhases.Main,TurnStartStep=null,TimingState=TimingStates.NeutralOpen,
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Battlefields=s.PlayerZones["P1"].Battlefields.Where(id=>id!="MUNDO").ToArray(),Hand=["MUNDO"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["MUNDO"]=new("P1","HAND")},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(mana:8,power:0,powerByTrait:new Dictionary<string,int>{["blue"]=2})}};
        var played=await Act(s,"P1",new PlayCardCommand("MUNDO","OGN·109/298",[]));Assert.Contains("MUNDO",played.State.PlayerZones["P1"].Base);
        Assert.Equal(6,played.State.CardObjects["MUNDO"].Power);Assert.Equal(8,Might(played.State));
        Assert.Equal(6,Assert.Single(played.Events,e=>e.Kind=="UNIT_PLAYED_TO_BASE").Payload["power"]);Restore(played.State);
    }

    [Fact]
    public async Task SwitcherooUsesCurrentMightSnapshotAndLaterGraveyardChangesStillApply()
    {
        var s=OfficialHoldSequenceTests.AddUnit(Position(3),"SMALL","OGN·001/298","F","P2");
        s=s with {Phase=MatchPhases.Main,TurnStartStep=null,TimingState=TimingStates.SpellDuelOpen,FocusPlayerId="P2",ActivePlayerId="P2",TurnPlayerId="P2",
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["SMALL"]=s.CardObjects["SMALL"] with {Power=1,IsAttacking=true},
                ["MUNDO"]=s.CardObjects["MUNDO"] with {IsDefending=true},
                ["SWAP"]=new("SWAP",cardNo:"SFD·145/221",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P2"]=s.PlayerZones["P2"] with {Hand=["SWAP"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["SWAP"]=new("P2","HAND")},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P2"]=new(2,2)}};
        var cast=await Act(s,"P2",new PlayCardCommand("SWAP","SFD·145/221",["MUNDO","SMALL"]));var done=await Top(cast.State);
        Assert.Equal(1,Might(done.State));Assert.Equal(-8,done.State.CardObjects["MUNDO"].UntilEndOfTurnPowerModifier);
        Assert.Equal(9,CurrentPowerTargetTests.Snapshot(done.State,"P2").GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("SMALL").GetProperty("effectivePower").GetInt32());Restore(done.State);
        var changed=done.State with {PlayerZones=new Dictionary<string,PlayerZones>(done.State.PlayerZones) {["P1"]=done.State.PlayerZones["P1"] with {Graveyard=[..done.State.PlayerZones["P1"].Graveyard,"G4"]}},
            CardObjects=new Dictionary<string,CardObjectState>(done.State.CardObjects) {["G4"]=new("G4",cardNo:"OGN·001/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(done.State.ObjectLocations) {["G4"]=new("P1","GRAVEYARD")}};
        Assert.Equal(2,Might(changed));Restore(changed);
    }

    [Fact]
    public async Task BothPlayersAndSpectatorRejectAlteredPublicRecycleContext()
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());var s=opened.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,opened.Snapshots[id],s.Tick,0,opened.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));var original=payload["beginningRecycle"];
            payload["beginningRecycle"]=s.StackItems[0].BeginningRecycle! with {Count=4};
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("beginning recycle context differs"));payload["beginningRecycle"]=original;
        }
    }
    [Fact]
    public async Task ClickOrderCannotChooseHiddenDeckOrderAndPublicRecycleListIsCanonical()
    {
        var opened=await Act(Position(),"P1",new PassPriorityCommand());var paused=await Top(opened.State);
        var a=await Choice(paused.State,"G5","G2","G4");var b=await Choice(paused.State,"G4","G5","G2");
        Assert.Equal(a.State.PlayerZones["P1"].MainDeck,b.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(a.State.RngCursor,b.State.RngCursor);
        Assert.Equal(new[]{"G2","G4","G5"},Assert.IsAssignableFrom<IEnumerable<string>>(Assert.Single(a.Events,e=>e.Kind=="CARDS_RECYCLED").Payload["cardIds"]));
        foreach(var id in new[]{"G2","G4","G5"}) Assert.True(a.State.CardObjects[id].ObjectGeneration>paused.State.CardObjects[id].ObjectGeneration);
        Restore(a.State);Restore(b.State);
    }
    [Fact]
    public async Task ActualImageCopyUsesPrintedMightAndItsNewControllersCurrentGraveyard()
    {
        var done=await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("OGN·109/298"));
        var id="C-TOKEN-001";Assert.Equal(6,done.State.CardObjects[id].Power);
        var expected=6+done.State.PlayerZones["P1"].Graveyard.Count;
        Assert.Equal(expected,CurrentPowerTargetTests.Snapshot(done.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32());
        Restore(done.State);
    }
}
