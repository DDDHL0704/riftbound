using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

// Explicit constructed transitions derived from a canonical command fixture.
// These four assertions do not claim an accepted command, natural card path,
// persistence birth, cold recovery, SQL or API execution for the transition.
public sealed class BattleLimitedRoleMemoMechanismTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Declaration = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
    [Theory]
    [InlineData("new-incarnation-and-public-coordinate-offset")]
    [InlineData("opposite-role-is-independent")]
    [InlineData("next-battle-with-previous-role-flags")]
    [InlineData("expiry-preserves-ordinary-eot")]
    public async Task ConstructedRoleMemoAndExpiryBoundariesRemainIndependent(string mode)
    {
        var fixture = await BattleLimitedRecoveryTests.ActualCopiedRoleState();
        var original = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(fixture, Json), Json)!;
        MatchState? before = null; MatchState? after = null; ResolutionResult? result = null;
        if(mode=="new-incarnation-and-public-coordinate-offset") {
            before=DetachSongs(original) with{StackItems=[],TriggerQueue=[],Tick=original.Tick+1};
            before=before with{CardObjects=new Dictionary<string,CardObjectState>(before.CardObjects){["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2") with{ObjectGeneration=2}}};
            before=Place(before,"ATTACKER","P2","BASE");after=Place(before,"ATTACKER","P2","BATTLEFIELD");
            result=Record(before,after,[new("INTERNAL_STABLE_MIGHT_INSTRUCTION","mechanical count fixture",new Dictionary<string,object?>()),new("UNIT_MOVED_TO_BATTLEFIELD","mechanical public row",new Dictionary<string,object?>()),new("INTERNAL_CARD_PLAY_COMPLETION","mechanical count fixture",new Dictionary<string,object?>())],7);
            var roles=Roles(result.State,"ATTACKER","ATTACKER");Assert.Equal(new long[]{1,2},roles.Select(r=>r["sourceGeneration"]!.GetValue<long>()).Order().ToArray());
            var fresh=roles.Single(r=>r["sourceGeneration"]!.GetValue<long>()==2);Assert.Equal(8,fresh["roleEventOrder"]!.GetValue<int>());
            Assert.Equal(2,Assert.Single(result.State.TriggerQueue).FieldContext!.SourceGeneration);
            Assert.Equal(1,ReadDeclaration(result.State)["PublicObjectBindings"]!.AsArray().Single(b=>b!["ObjectId"]!.GetValue<string>()=="ATTACKER")!["Generation"]!.GetValue<long>());
            Assert.Single(result.Events,e=>e.Kind=="BATTLE_ROLE_ACQUIRED");
        }
        else if(mode=="opposite-role-is-independent") {
            before=original with{StackItems=[],TriggerQueue=[]};after=DetachSongs(before) with{Tick=before.Tick+1};
            after=Place(after,"ATTACKER","P1","BATTLEFIELD");after=Place(after,"HOST","P2","BATTLEFIELD");result=Record(before,after,[],0);
            Assert.Equal("P2",Assert.Single(Roles(result.State,"ATTACKER","ATTACKER"))["controllerId"]!.GetValue<string>());
            Assert.Equal("P1",Assert.Single(Roles(result.State,"ATTACKER","DEFENDER"))["controllerId"]!.GetValue<string>());
            Assert.True(result.State.CardObjects["ATTACKER"].IsDefending);Assert.True(result.State.CardObjects["HOST"].IsAttacking);
            Assert.Equal("BATTLE_DOUBLE_SELF_DEFEND",Assert.Single(result.State.TriggerQueue).FieldContext!.Kind);
        }
        else if(mode=="next-battle-with-previous-role-flags") {
            before=original with{StackItems=[],TriggerQueue=[]};after=Expire(before) with{Tick=before.Tick+1};
            var declaration=ReadDeclaration(after);declaration["InitialRolesEstablished"]=false;declaration["CombatStarted"]=false;
            var marker=Declaration+Convert.ToBase64String(Encoding.UTF8.GetBytes(declaration.ToJsonString()));
            after=after with{UntilEndOfTurnEffects=after.UntilEndOfTurnEffects.Where(t=>!t.StartsWith(Declaration)).Append(marker).ToArray()};
            result=Record(before,after,[],0);Assert.Equal(3,result.State.TriggerQueue.Count);
            Assert.Equal(3,result.State.CardObjects["ATTACKER"].Power);
            var first=Assert.Single(Roles(result.State,"ATTACKER","ATTACKER"));Assert.Equal(after.Tick,first["scope"]!["startedAtTick"]!.GetValue<long>());
            Assert.Single(result.Events,e=>e.Kind=="BATTLE_ROLES_ESTABLISHED");
        }
        else {
            var card=original.CardObjects["ATTACKER"];var old=card.UntilEndOfTurnPowerModifier;
            var ordinary=new PowerModifierLedgerEntry("QA_ORDINARY_EOT","QA_ORDINARY_EOT","UNTIL_END_OF_TURN","ATTACKER","HOST","OGN·210/298",2,card.Power-old,card.Power+2,"EXPLICIT_MECHANISM_FIXTURE",2,0,card.Power+2,4);
            before=original with{CardObjects=new Dictionary<string,CardObjectState>(original.CardObjects){["ATTACKER"]=card with{Power=card.Power+2,UntilEndOfTurnPowerModifier=old+2,UntilEndOfTurnPowerModifiers=[..card.UntilEndOfTurnPowerModifiers,ordinary],UntilEndOfTurnEffects=[..card.UntilEndOfTurnEffects,"COMBAT_KEYWORD:坚守3:1"]}}};
            after=Expire(before);var remaining=after.CardObjects["ATTACKER"];Assert.Equal(5,remaining.Power);Assert.Equal(2,remaining.UntilEndOfTurnPowerModifier);
            Assert.Equal(ordinary,Assert.Single(remaining.UntilEndOfTurnPowerModifiers));Assert.Contains("COMBAT_KEYWORD:坚守3:1",remaining.UntilEndOfTurnEffects);
            Assert.DoesNotContain(remaining.UntilEndOfTurnEffects,t=>t.StartsWith("BATTLE_LIMITED_GRANT:"));
        }
    }

    private static ResolutionResult Record(MatchState before,MatchState after,IReadOnlyList<GameEvent> events,int offset)
        =>(ResolutionResult)typeof(CoreRuleEngine).GetMethod("RecordBattleDuelRoles",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[before,new ResolutionResult(true,null,after,events,ResolutionResult.BuildSnapshots(after),ResolutionResult.BuildPrompts(after)),offset])!;
    private static MatchState Expire(MatchState state)=>(MatchState)typeof(CoreRuleEngine).GetMethod("ExpireBattleLimitedScope",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[state,"HOME"])!;
    private static JsonObject ReadDeclaration(MatchState state)=>JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(state.UntilEndOfTurnEffects.Single(t=>t.StartsWith(Declaration))[Declaration.Length..])))!.AsObject();
    private static JsonObject[] Roles(MatchState state,string source,string role)=>state.UntilEndOfTurnEffects.Where(t=>t.StartsWith("BATTLE_ROLE_FIRST:")).Select(t=>JsonNode.Parse(t["BATTLE_ROLE_FIRST:".Length..])!.AsObject()).Where(r=>r["sourceObjectId"]!.GetValue<string>()==source&&r["role"]!.GetValue<string>()==role).ToArray();
    private static MatchState DetachSongs(MatchState state){foreach(var id in new[]{"SONG-1","SONG-2"}){state=state with{CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){[id]=state.CardObjects[id] with{AttachedToObjectId=null,AttachedRuleText=null}}};state=Place(state,id,"P2","BASE");}return state;}
    private static MatchState Place(MatchState state,string id,string controller,string zone){var zones=state.PlayerZones.ToDictionary(p=>p.Key,p=>p.Value with{Base=p.Value.Base.Where(x=>x!=id).ToArray(),Battlefields=p.Value.Battlefields.Where(x=>x!=id).ToArray()});zones[controller]=zone=="BASE"?zones[controller] with{Base=[..zones[controller].Base,id]}:zones[controller] with{Battlefields=[..zones[controller].Battlefields,id]};return state with{PlayerZones=zones,CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){[id]=state.CardObjects[id] with{ControllerId=controller}},ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){[id]=new(controller,zone,zone=="BASE"?null:"HOME")}};}
}
