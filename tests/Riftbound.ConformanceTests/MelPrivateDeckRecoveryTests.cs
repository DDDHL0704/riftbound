using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MelPrivateDeckRecoveryTests
{
    private static async Task<MatchState> Pending()
    {
        var s=MelRulesTests.Spell(await MelRulesTests.Enter(),"OGN·048/298");
        s=MelRulesTests.Spell(s,"UNL-131/219","COUNTER","P2");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["PRIVATE-TOP"]=new("PRIVATE-TOP",cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=s.PlayerZones["P2"] with { MainDeck=["PRIVATE-TOP"] } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["PRIVATE-TOP"]=new("P2","MAIN_DECK") } };
        var cast=await Act(s,"P1",new PlayCardCommand("SPELL","OGN·048/298",[]));
        var passed=await Act(cast.State,"P1",new PassPriorityCommand());
        var done=await Top((await Act(passed.State,"P2",new PlayCardCommand("COUNTER","UNL-131/219",[cast.State.StackItems.Single().StackItemId]))).State);
        Assert.NotNull(done.State.PendingCardChoice!.DeckContext);Assert.Equal("P2",done.State.PendingCardChoice.PlayerId);
        Restore(done.State);ArmamentMightTests.PublicRecovery(done.State);return done.State;
    }
    private static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("requiredCount")]
    [InlineData("maxCount")]
    [InlineData("legalCount")]
    [InlineData("legalObjectIds")]
    [InlineData("viewedCards")]
    [InlineData("playerId")]
    public async Task RecoveryRejectsPrivateDeckInformationOrForgedWaitingIdentityInOpponentAndSpectatorViews(string field)
    {
        var s=await Pending();var snapshots=ResolutionResult.BuildSnapshots(s).ToDictionary(p=>p.Key,p=>p.Value);
        var json=JsonNode.Parse(JsonSerializer.Serialize(snapshots["P1"],Options))!;
        var choice=json["timing"]!["pendingCardChoice"]!.AsObject();
        foreach(var key in new[] {"requiredCount","maxCount","legalCount","legalObjectIds","viewedCards"})Assert.False(choice.ContainsKey(key));
        if(field=="playerId")choice[field]="P1";
        else if(field is "legalObjectIds" or "viewedCards")choice[field]=new JsonArray("PRIVATE-TOP");
        else choice[field]=1;
        snapshots["P1"]=json.Deserialize<SnapshotDto>(Options)!;
        var prompts=ResolutionResult.BuildPrompts(s);var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,snapshots[id],s.Tick,0,prompts[id]));
        Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick),e=>e.Contains("private deck choice"));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var spectatorJson=JsonNode.Parse(JsonSerializer.Serialize(spectator.SpectatorSnapshot,Options))!;
        spectatorJson["timing"]!["pendingCardChoice"]![field]=choice[field]!.DeepClone();
        var bad=spectator with {SpectatorSnapshot=spectatorJson.Deserialize<SnapshotDto>(Options)!};
        Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),s,s.Tick,bad),e=>e.Contains("private deck choice"));
    }
    [Theory]
    [InlineData("requiredCount")]
    [InlineData("maxCount")]
    [InlineData("legalCount")]
    public async Task ChoosingPlayerStillRequiresThePrivateCounts(string field)
    {
        var s=await Pending();var snapshots=ResolutionResult.BuildSnapshots(s).ToDictionary(p=>p.Key,p=>p.Value);
        var json=JsonNode.Parse(JsonSerializer.Serialize(snapshots["P2"],Options))!;var choice=json["timing"]!["pendingCardChoice"]!.AsObject();
        Assert.True(choice.ContainsKey(field));Assert.Equal("PENDING_CHOICE",choice["choiceState"]!.GetValue<string>());choice.Remove(field);
        snapshots["P2"]=json.Deserialize<SnapshotDto>(Options)!;var prompts=ResolutionResult.BuildPrompts(s);
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,snapshots[id],s.Tick,0,prompts[id]));
        Assert.NotEmpty(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick));
    }
}
