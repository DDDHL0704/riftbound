using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class MovementThresholdTests
{
    internal static MatchState Position(string cardNo="OGN·205/298")
    {
        var s=SequentialMovementTests.Position();
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo=cardNo,Power=10},
            ["KEEPER"]=new("KEEPER",cardNo:"OGN·096/298",power:20,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])}};
        return At(s,"KEEPER","P2","BATTLEFIELD","BF");
    }
    internal static async Task<ResolutionResult> Move(MatchState state,int ordinal)
    {
        var id="MOVE-"+ordinal;var card=state.CardObjects["AOE"] with {ObjectId=id};
        var s=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects) {[id]=card},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones) {["P1"]=state.PlayerZones["P1"] with {Hand=[..state.PlayerZones["P1"].Hand,id]}},
            RunePools=new Dictionary<string,RunePool>(state.RunePools) {["P1"]=new(20,20)},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations) {[id]=new("P1","HAND")}};
        return await Top((await Act(s,"P1",new PlayCardCommand(id,SequentialMovementTests.Spell,["A","D"],
            MovementDestinations:ordinal%2==1?["BATTLEFIELD:BF2","BASE:P2"]:["BASE:P1","BATTLEFIELD:BF"]))).State);
    }
    [Theory]
    [InlineData("OGN·205/298")] [InlineData("OGN·205a/298")] [InlineData("SFD·235/221")] [InlineData("SFD·235*/221")]
    public async Task ThirdActualMoveQueuesScoreForEveryCurrentYasuoPrinting(string no)
    {
        var first=await Move(Position(no),1);Assert.Empty(first.State.StackItems);Assert.Equal(0,first.State.PlayerScores.GetValueOrDefault("P1"));
        var second=await Move(first.State,2);Assert.Empty(second.State.StackItems);
        var third=await Move(second.State,3);Assert.Single(third.State.StackItems);Assert.Equal(0,third.State.PlayerScores.GetValueOrDefault("P1"));Restore(third.State);
        var done=await Top(third.State);Assert.Equal(1,done.State.PlayerScores["P1"]);Restore(done.State);
        var fourth=await Move(done.State,4);Assert.Empty(fourth.State.StackItems);Assert.Equal(1,fourth.State.PlayerScores["P1"]);
    }
    public static IEnumerable<object[]> KaynMoves() => new[] { "OGN·189/298", "RAD·182*/167", "RAD·182/167" }
        .SelectMany(no => new[] { 1, 2, 3 }.Select(moves => new object[] { no, moves, moves == 1 ? 6 : 0 }));
    [Theory]
    [MemberData(nameof(KaynMoves))]
    public async Task KaynIsImmuneOnlyAfterTwoActualMoves(string cardNo,int moves,int damage)
    {
        var s=Position(cardNo);for(var i=1;i<=moves;i++)s=(await Move(s,i)).State;
        Assert.Equal(moves,s.CardObjects["A"].MovementCountThisTurn);
        const string no="OGN·252/298";s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["HIT"]=new("HIT",cardNo:no,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Hand=[..s.PlayerZones["P1"].Hand,"HIT"]}}};
        var done=await Top((await Act(s,"P1",new PlayCardCommand("HIT",no,["A"]))).State);Assert.True(damage==done.State.CardObjects["A"].Damage,System.Text.Json.JsonSerializer.Serialize(new {card=done.State.CardObjects["A"],done.Events,done.State.StackItems,done.State.PendingRuleChoice}));Restore(done.State);
    }

    [Fact]
    public async Task ThirdMoveCanWinWithoutConqueringEveryBattlefield()
    {
        var s=Position() with {PlayerScores=new Dictionary<string,int> {["P1"]=7,["P2"]=0}};
        for(var i=1;i<=3;i++)s=(await Move(s,i)).State;
        Assert.Null(s.WinnerPlayerId);var done=await Top(s);
        Assert.Equal("P1",done.State.WinnerPlayerId);Assert.Equal(8,done.State.PlayerScores["P1"]);
        Assert.Single(done.Events,e=>e.Kind=="MATCH_WON");Restore(done.State);
    }
    [Fact]
    public async Task CapturedThirdMoveKeepsControllerAfterSourceChangesControl()
    {
        var s=Position();for(var i=1;i<=3;i++)s=(await Move(s,i)).State;
        s=At(s,"A","P2","BASE");var done=await Top(s);
        Assert.Equal(1,done.State.PlayerScores["P1"]);Assert.Equal(0,done.State.PlayerScores["P2"]);
        Assert.Equal(3,done.State.CardObjects["A"].MovementCountThisTurn);Restore(done.State);
    }
    [Fact]
    public async Task CountSurvivesRestoreAndHashIncludesIt()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;
        var restored=OfficialInsightAndSpellLockTests.Restore(s);Assert.Equal(1,restored.CardObjects["A"].MovementCountThisTurn);
        var changed=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {MovementCountThisTurn=2}}};
        Assert.NotEqual(MatchStateHasher.Hash(s),MatchStateHasher.Hash(changed));
        var second=await Move(restored,2);Assert.Equal(2,second.State.CardObjects["A"].MovementCountThisTurn);Restore(second.State);
        var malformed=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {MovementCountThisTurn=-1}}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(malformed),e=>e.Contains("movement count"));
    }
    [Fact]
    public async Task EndingTurnResetsEveryUnitsMovementCount()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;s=(await Move(s,2)).State;
        var end=await Act(s,"P1",new EndTurnCommand());
        Assert.All(end.State.CardObjects.Values,c=>Assert.Equal(0,c.MovementCountThisTurn));Restore(end.State);
    }
    [Fact]
    public async Task FailedMovementDoesNotIncrementCountOrMutateState()
    {
        var s=(await Move(Position(),1)).State;var cmd=new MoveUnitCommand("A","BATTLEFIELD:BF2","BATTLEFIELD:BF2",[]);
        var bad=await new CoreRuleEngine().ResolveAsync(s,new("bad","P1",cmd.CmdType),cmd,default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(bad.State));
    }
    internal static async Task<ResolutionResult> ImmuneCombat(bool immune=true,bool only=false)
    {
        var s=DeathAndDuelLifecycleTests.State();
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo="OGN·096/298",Power=5},
            ["D"]=s.CardObjects["D"] with {CardNo="OGN·189/298",Power=2,MovementCountThisTurn=immune?2:1,Tags=[CardObjectTags.UnitCard,CardCombatKeywordNames.Bulwark]},
            ["OTHER"]=new("OTHER",cardNo:"OGN·096/298",power:1,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) }};
        s=At(s,"OTHER","P2",only?"BASE":"BATTLEFIELD",only?null:"BF");
        var m=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var f=await Act(m.State,"P1",new PassFocusCommand());f=await Act(f.State,"P2",new PassFocusCommand());
        var opened=await Act(f.State,"P1",new DeclareBattleCommand("BF",["A"],only?["D"]:["D","OTHER"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;i<12 && opened.State.PriorityPlayerId is not null;i++)opened=await Act(opened.State,opened.State.PriorityPlayerId,new PassPriorityCommand());
        return opened;
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ImmuneBulwarkIsExemptFromMandatoryAssignment(bool immune)
    {
        var opened=await ImmuneCombat(immune);var b=opened.State.BattleState;
        var cmd=new AssignCombatDamageCommand(b.BattleId!,"BF",[new("A","OTHER",5)]);
        var assigned=await new CoreRuleEngine().ResolveAsync(opened.State,new("assign","P1",cmd.CmdType),cmd,default);
        Assert.Equal(immune,assigned.Accepted);
        if(!immune){Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(assigned.State));return;}
        var done=await CombatTestDriver.FinishAsync(assigned);Assert.Contains("OTHER",done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("D",done.State.PlayerZones["P2"].Battlefields);Assert.Equal(0,done.State.CardObjects["D"].Damage);Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SuggestedCombatAssignmentsSkipImmuneUnitsUnlessTheyAreTheOnlyTargets(bool only)
    {
        var opened=await ImmuneCombat(only:only);
        if(only){Assert.False(opened.State.BattleState.IsActive);Assert.Contains("D",opened.State.PlayerZones["P2"].Battlefields);Assert.Equal(0,opened.State.CardObjects["D"].Damage);Restore(opened.State);return;}
        var candidate=Assert.Single(opened.Prompts["P1"].Candidates!,c=>c.Action==CommandTypes.AssignCombatDamage);
        var metadata=Assert.IsAssignableFrom<IReadOnlyDictionary<string,object?>>(candidate.Metadata);
        Assert.Equal(new[]{"D"},Assert.IsType<string[]>(metadata["damageImmuneTargetIds"]));
        var suggestion=Assert.IsAssignableFrom<IReadOnlyDictionary<string,long>>(metadata["suggestedDamageByTarget"]);
        Assert.Equal(5,suggestion[only?"D":"OTHER"]);if(!only)Assert.False(suggestion.ContainsKey("D"));
        var done=await CombatTestDriver.FinishAsync(opened);Assert.Contains("D",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }

    internal static async Task<ResolutionResult> Cast(MatchState s,string no,params string[] targets)
    {
        var id="CAST-"+s.Tick;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {[id]=new(id,cardNo:no,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Hand=[..s.PlayerZones["P1"].Hand,id]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {[id]=new("P1","HAND")},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)}};
        return await Top((await Act(s,"P1",new PlayCardCommand(id,no,targets))).State);
    }
    [Fact]
    public async Task ReturningToHandCreatesFreshObjectWithoutMovementHistory()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;s=(await Move(s,2)).State;
        var generation=s.CardObjects["A"].ObjectGeneration;
        var bounced=await Cast(s,"UNL-128/219","A","D");Assert.Contains("A",bounced.State.PlayerZones["P1"].Hand);
        Assert.Equal(0,bounced.State.CardObjects["A"].MovementCountThisTurn);Assert.True(bounced.State.CardObjects["A"].ObjectGeneration>generation);Restore(bounced.State);
        var replayed=await Act(bounced.State,"P1",new PlayCardCommand("A","OGN·189/298",[]));
        if(replayed.State.StackItems.Count>0)replayed=await Top(replayed.State);
        Assert.Contains("A",replayed.State.PlayerZones["P1"].Base);Assert.Equal(0,replayed.State.CardObjects["A"].MovementCountThisTurn);Restore(replayed.State);
    }
    [Fact]
    public async Task ActualControlChangeKeepsTheSameObjectsMovementHistory()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;s=(await Move(s,2)).State;
        var generation=s.CardObjects["D"].ObjectGeneration;var taken=await Cast(s,"SFD·202/221","D");
        Assert.Equal("P1",taken.State.CardObjects["D"].ControllerId);Assert.Equal(2,taken.State.CardObjects["D"].MovementCountThisTurn);
        Assert.Equal(generation,taken.State.CardObjects["D"].ObjectGeneration);Restore(taken.State);
    }
    [Fact]
    public async Task MovementImmunityDoesNotConsumeNextDamagePrevention()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;s=(await Move(s,2)).State;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {UntilEndOfTurnEffects=["PREVENT_NEXT_DAMAGE_THIS_TURN"]}}};
        var hit=await Cast(s,"OGN·252/298","A");Assert.Equal(0,hit.State.CardObjects["A"].Damage);
        Assert.Contains("PREVENT_NEXT_DAMAGE_THIS_TURN",hit.State.CardObjects["A"].UntilEndOfTurnEffects);Restore(hit.State);
    }
    [Fact]
    public async Task ActualBlockCannotForceDamageAssignmentToImmuneKayn()
    {
        var s=DeathAndDuelLifecycleTests.State();
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo="OGN·096/298",Power=5},
            ["D"]=s.CardObjects["D"] with {CardNo="OGN·189/298",Power=2,MovementCountThisTurn=2},
            ["OTHER"]=new("OTHER",cardNo:"OGN·096/298",power:1,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) }};
        s=At(s,"OTHER","P2","BATTLEFIELD","BF");s=(await Cast(s,"OGN·057/298","D")).State;
        Assert.Equal(2,s.CardObjects["D"].MovementCountThisTurn);
        var m=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var f=await Act(m.State,"P1",new PassFocusCommand());f=await Act(f.State,"P2",new PassFocusCommand());
        var opened=await Act(f.State,"P1",new DeclareBattleCommand("BF",["A"],["D","OTHER"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;i<12 && opened.State.PriorityPlayerId is not null;i++)opened=await Act(opened.State,opened.State.PriorityPlayerId,new PassPriorityCommand());
        var assigned=await Act(opened.State,"P1",new AssignCombatDamageCommand(opened.State.BattleState.BattleId!,"BF",[new("A","OTHER",5)]));
        var done=await CombatTestDriver.FinishAsync(assigned);Assert.Contains("D",done.State.PlayerZones["P2"].Battlefields);Assert.Contains("OTHER",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }

    [Theory]
    [InlineData(0,1)] [InlineData(1,1)] [InlineData(1,2)]
    public async Task BlockGrantsStackAndApplyWhenUnitLaterBecomesDefender(int printed,int casts)
    {
        var s=DeathAndDuelLifecycleTests.State();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo="OGN·096/298",Power=20},
            ["D"]=s.CardObjects["D"] with {Power=2,Tags=printed==0?[CardObjectTags.UnitCard]:[CardObjectTags.UnitCard,CardCombatKeywordNames.Steadfast]},
            ["OTHER"]=new("OTHER",cardNo:"OGN·096/298",power:1,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) }};
        s=At(s,"OTHER","P2","BATTLEFIELD","BF");
        for(var i=0;i<casts;i++)s=(await Cast(s,"OGN·057/298","D")).State;
        Assert.Equal(2,s.CardObjects["D"].Power);Assert.Equal(casts*2,s.CardObjects["D"].UntilEndOfTurnEffects.Count);Restore(s);
        var m=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var f=await Act(m.State,"P1",new PassFocusCommand());f=await Act(f.State,"P2",new PassFocusCommand());
        var opened=await Act(f.State,"P1",new DeclareBattleCommand("BF",["A"],["D","OTHER"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;i<12 && opened.State.PriorityPlayerId is not null;i++)opened=await Act(opened.State,opened.State.PriorityPlayerId,new PassPriorityCommand());
        var metadata=System.Text.Json.JsonSerializer.SerializeToElement(opened.Prompts["P1"].View!.Metadata);
        Assert.Equal(2+printed+3*casts,metadata.GetProperty("lethalDamageThreshold").GetProperty("D").GetInt32());
        Assert.Equal(0,metadata.GetProperty("targetPriority").GetProperty("D").GetInt32());Restore(opened.State);
    }
    [Fact]
    public async Task RecallPreservesObjectWithoutCountingAsMovement()
    {
        var s=(await Move(Position("OGN·189/298"),1)).State;s=(await Move(s,2)).State;
        var generation=s.CardObjects["D"].ObjectGeneration;var recalled=await Cast(s,"UNL-140/219","D");
        Assert.Contains("D",recalled.State.PlayerZones["P1"].Base);Assert.Equal(2,recalled.State.CardObjects["D"].MovementCountThisTurn);
        Assert.Equal(generation,recalled.State.CardObjects["D"].ObjectGeneration);Restore(recalled.State);
    }
}
