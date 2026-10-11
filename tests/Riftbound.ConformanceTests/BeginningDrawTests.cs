using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class BeginningDrawTests
{
    internal static MatchState Jinx(int hand=0,string card="OGN·251/298")
    {
        var s=OfficialHoldSequenceTests.State("OGN·294/298");var held=s.PlayerZones["P1"].MainDeck.Take(hand).ToArray();
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["JINX"]=new("JINX",cardNo:card,ownerId:"P1",controllerId:"P1",tags:["CARD_TYPE:LEGEND"]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {LegendZone=["JINX"],Hand=held,MainDeck=s.PlayerZones["P1"].MainDeck.Skip(hand).ToArray()}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations.Concat(held.Select(id=>new KeyValuePair<string,ObjectLocationState>(id,new("P1","HAND")))).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Last().Value)) {["JINX"]=new("P1","LEGEND")} };
    }
    internal static Task<ResolutionResult> Start(MatchState s)=>Act(s,s.TurnPlayerId,new PassPriorityCommand());
    [Theory]
    [InlineData(0,2)] [InlineData(1,3)] [InlineData(2,3)]
    public async Task JinxAlwaysTriggersBeforeNormalDrawAndChecksHandOnlyOnResolution(int hand,int final)
    {
        var opened=await Start(Jinx(hand));Assert.Single(opened.State.StackItems);Assert.Equal(hand,opened.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal("SCORE",opened.State.TurnStartStep);Restore(opened.State);
        var done=await Top(opened.State);Assert.Equal(final,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    internal static MatchState Mushroom(bool hidden=true)
    {
        var s=OfficialHoldSequenceTests.State("OGN·294/298");s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["POUCH"]=new("POUCH",cardNo:"OGN·101/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]),
            ["STANDBY-CARD"]=new("STANDBY-CARD",cardNo:"OGN·048/298",ownerId:"P1",controllerId:"P1",isFaceDown:true,tags:[CardObjectTags.SpellCard,CardObjectTags.Standby]) }};
        s=At(s,"POUCH","P1","BASE");return hidden?At(s,"STANDBY-CARD","P1","BATTLEFIELD","F"):s;
    }
    [Fact]
    public async Task MushroomDrawHasAResponseWindowBeforeScoring()
    {
        var opened=await Start(Mushroom());Assert.Single(opened.State.StackItems);Assert.Empty(opened.State.PlayerZones["P1"].Hand);Restore(opened.State);
        var done=await Top(opened.State);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·251/298")] [InlineData("OGN·301/298")] [InlineData("OGN·301*/298")] [InlineData("FND-251/298")]
    public async Task AllCurrentJinxPrintsUseTheSameCapturedSkill(string card)
    {
        var opened=await Start(Jinx(1,card));Assert.Equal(card,Assert.Single(opened.State.StackItems).BeginningDraw!.CardNo);
        var done=await Top(opened.State);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Theory]
    [InlineData("base")] [InlineData("opponent")] [InlineData("face-up")] [InlineData("unknown-field")]
    public async Task MushroomDoesNotTriggerForIneligibleHiddenObjects(string branch)
    {
        var s=Mushroom();
        if(branch=="base")s=At(s,"STANDBY-CARD","P1","BASE");
        if(branch=="opponent")s=At(s,"STANDBY-CARD","P2","BATTLEFIELD","OTHER");
        if(branch=="unknown-field")s=At(s,"STANDBY-CARD","P1","BATTLEFIELD","NO-FIELD");
        if(branch=="face-up")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["STANDBY-CARD"]=s.CardObjects["STANDBY-CARD"] with {IsFaceDown=false,Tags=[CardObjectTags.UnitCard]}}};
        var done=await Start(s);Assert.Empty(done.State.StackItems);Assert.Single(done.State.PlayerZones["P1"].Hand);
    }
    [Theory]
    [InlineData("hidden-leaves")] [InlineData("source-leaves")] [InlineData("source-controller")]
    public async Task MushroomDoesNotRecheckCapturedConditionOrSourceOnResolution(string change)
    {
        var opened=await Start(Mushroom());var s=opened.State;
        if(change=="hidden-leaves")s=At(s,"STANDBY-CARD","P1","BASE");
        if(change=="source-controller")s=At(s,"POUCH","P2","BASE");
        if(change=="source-leaves")s=s with {
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Base=[],Graveyard=["POUCH"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["POUCH"]=new("P1","GRAVEYARD")},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["POUCH"]=s.CardObjects["POUCH"] with {ObjectGeneration=1}} };
        var done=await Top(s);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Theory]
    [InlineData("hidden")] [InlineData("wrong-zone")] [InlineData("opponent")]
    public async Task JinxRequiresAVisibleOwnLegendAtTriggerTime(string change)
    {
        var s=Jinx();
        if(change=="hidden")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["JINX"]=s.CardObjects["JINX"] with {IsFaceDown=true}}};
        if(change=="opponent")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["JINX"]=s.CardObjects["JINX"] with {ControllerId="P2"}}};
        if(change=="wrong-zone")s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {LegendZone=[],Hand=["JINX"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["JINX"]=new("P1","HAND")} };
        var done=await Start(s);Assert.Empty(done.State.StackItems);Assert.Equal(change=="wrong-zone"?2:1,done.State.PlayerZones["P1"].Hand.Count);
    }
    [Fact]
    public async Task JinxRespondsToNewCardsInHandAndSkipsItsDrawAtResolution()
    {
        var opened=await Start(Jinx(1));var s=opened.State;var id=s.PlayerZones["P1"].MainDeck[0];
        // Isolated response-state hand growth; actual hand reduction is covered by native Flash.
        s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {MainDeck=s.PlayerZones["P1"].MainDeck.Skip(1).ToArray(),Hand=[..s.PlayerZones["P1"].Hand,id]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {[id]=new("P1","HAND")} };
        var done=await Top(s);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Assert.Single(done.Events,e=>e.Kind=="CARD_DRAWN");Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task JinxAndMushroomRespectChosenOrderBeforeTheNormalDraw(bool mushroomFirst)
    {
        var s=Jinx(1);var m=Mushroom();
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["POUCH"]=m.CardObjects["POUCH"],["STANDBY-CARD"]=m.CardObjects["STANDBY-CARD"]}};
        s=At(At(s,"POUCH","P1","BASE"),"STANDBY-CARD","P1","BATTLEFIELD","F");
        var opened=await Start(s);Assert.Equal(2,opened.State.TriggerQueue.Count);Restore(opened.State);
        var ordered=opened.State.TriggerQueue.OrderBy(t=>t.SourceObjectId==(mushroomFirst?"POUCH":"JINX")?0:1).Select(t=>t.TriggerId).ToArray();
        var result=await TurnSequenceTestDriver.Complete(await Act(opened.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:ordered)));
        Assert.Equal(mushroomFirst?3:4,result.State.PlayerZones["P1"].Hand.Count);Restore(result.State);
    }
    [Theory]
    [InlineData("kind")] [InlineData("card")] [InlineData("generation")] [InlineData("missing")] [InlineData("controller")]
    public async Task ForgedBeginningDrawContextRejectsRecovery(string change)
    {
        var opened=await Start(Jinx());var item=opened.State.StackItems.Single();var c=item.BeginningDraw!;
        var bad=item with {BeginningDraw=change switch {"kind"=>c with {Kind="FACEDOWN_AT_START"},"card"=>c with {CardNo="OGN·001/298"},"generation"=>c with {SourceGeneration=-1},"missing"=>null,_=>c},
            ControllerId=change=="controller"?"P2":item.ControllerId};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with {StackItems=[bad]}),e=>e.Contains("captured beginning draw"));
    }
    [Fact]
    public async Task BothPlayersAndSpectatorRejectAlteredPublicBeginningDrawContext()
    {
        var opened=await Start(Jinx());var s=opened.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,opened.Snapshots[id],s.Tick,0,opened.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));var original=payload["beginningDraw"];
            payload["beginningDraw"]=s.StackItems[0].BeginningDraw! with {Kind="FACEDOWN_AT_START"};
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("beginning draw context differs"));payload["beginningDraw"]=original;
        }
    }

    [Theory]
    [InlineData("controller")] [InlineData("source-leaves")]
    public async Task CapturedJinxKeepsItsControllerAfterSourceChanges(string change)
    {
        var opened=await Start(Jinx(1));var s=opened.State;
        if(change=="controller")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["JINX"]=s.CardObjects["JINX"] with {ControllerId="P2"}}};
        else s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["JINX"]=s.CardObjects["JINX"] with {ObjectGeneration=1}},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {LegendZone=[],Banished=["JINX"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["JINX"]=new("P1","BANISHED")} };
        var done=await Top(s);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Assert.Empty(done.State.PlayerZones["P2"].Hand);Restore(done.State);
    }
    [Fact]
    public async Task MushroomWithoutAFacedownCardDoesNotQueue()
    {
        var done=await Start(Mushroom(false));Assert.Empty(done.State.StackItems);Assert.Single(done.State.PlayerZones["P1"].Hand);Restore(done.State);
    }
}
