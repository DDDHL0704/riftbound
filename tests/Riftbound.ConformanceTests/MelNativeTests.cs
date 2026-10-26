using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MelNativeTests
{
    [Theory]
    [InlineData("minimum","VEN·069")]
    [InlineData("moonfall","VEN·069")]
    [InlineData("counter","VEN·069a")]
    [InlineData("ability","VEN·069a")]
    [InlineData("control","VEN·069b")]
    public async Task ProductionControlsResolveEmpoweredSpellsAndAbilitiesAfterColdRecovery(string branch,string cardNo)
    {
        var initial = MelRulesTests.Position(cardNo);
        if (branch=="moonfall") {
            var board=GroupedTargetTests.OtherField(GroupedTargetTests.Group(3,5),"D2");
            board=LocalDestructionRecallTests.At(board,"D3","P2","BASE");
            initial=MelRulesTests.Position(cardNo,board);
        } else initial=MelRulesTests.Enemy(initial,branch=="minimum" ? 2 : 5);
        initial=MelRulesTests.Spell(initial,branch=="moonfall" ? MoonfallTests.Spell : branch=="counter" ? "OGN·048/298" : "OGN·095/298","ACTION");
        if (branch is "counter" or "ability" or "control")
            initial=MelRulesTests.Spell(initial,branch=="counter" ? "UNL-131/219" : branch=="ability" ? "SFD·045/221" : "OGN·080/298","RESPONSE","P2");
        if (branch=="ability") initial=initial with {
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) { ["ORB"]=new("ORB",cardNo:OrbOfRegretTests.Card,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"]=initial.PlayerZones["P1"] with { Base=[..initial.PlayerZones["P1"].Base,"ORB"] } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["ORB"]=new("P1","BASE") } };
        var cards=initial.CardObjects.ToDictionary(p=>p.Key,p=>p.Value);var locations=initial.ObjectLocations.ToDictionary(p=>p.Key,p=>p.Value);
        var p2deck=Enumerable.Range(0,3).Select(i=>"MEL-P2-DECK-"+i).ToArray();
        foreach (var id in p2deck) {cards[id]=new(id,cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]);locations[id]=new("P2","MAIN_DECK");}
        initial=initial with {CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P2"]=initial.PlayerZones["P2"] with { MainDeck=p2deck } }};
        Restore(initial);ArmamentMightTests.PublicRecovery(initial);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_MEL_EVIDENCE");var dir=root is null ? null : Path.Combine(root,branch);
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web) {WriteIndented=true,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull};
        var engine=new CoreRuleEngine();var journal=new Journal();var session=new MatchSession(initial,engine,journal);var current=initial;
        var steps=new List<object>();var native=0;var coldRestores=0;
        void Export(string name,object value) {if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),options));}
        async Task Submit(string name,string actor,GameCommand fallback) {
            var prompt=session.PromptFor(actor);Export(name+"-prompt.json",prompt);Export(name+"-snapshot.json",ResolutionResult.BuildSnapshots(current)[actor]);Export(name+"-selection.json",fallback);
            var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(fallback,fallback.GetType(),options),options)!;
            fields["promptId"]=JsonSerializer.SerializeToElement(prompt.PromptId);fields["snapshotTick"]=JsonSerializer.SerializeToElement(current.Tick);
            var raw=JsonSerializer.SerializeToElement(fields);var command=fallback;var path=dir is null ? null : Path.Combine(dir,name+"-command.json");
            if(path is not null&&File.Exists(path)) {using var doc=JsonDocument.Parse(File.ReadAllText(path));raw=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);native++;}
            if(fallback is PlayCardCommand play)Export(name+"-quote.json",engine.PreviewPlayCard(current,actor,new("quote",prompt.PromptId!,current.Tick,play)));
            steps.Add(new {name,action=fallback.CmdType});var done=await session.SubmitAsync(actor,name,command,raw,default);Assert.True(done.Accepted,done.ErrorMessage);
            var entries=journal.Entries.Count;Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted);Assert.Equal(entries,journal.Entries.Count);
            current=done.State;Restore(current);ArmamentMightTests.PublicRecovery(current);
            if(name=="empower") {
                var stale=await session.SubmitAsync(actor,"stale",command,raw,default);Assert.False(stale.Accepted);Assert.Equal(MatchStateHasher.Hash(current),MatchStateHasher.Hash(stale.State));
            }
            if(name=="empower" || current.PendingCardChoice?.DeckContext is not null) {
                current=OfficialInsightAndSpellLockTests.Restore(current);
                var commands=Commands(journal);var events=Events(journal);var sequence=journal.Entries[^1].CompletedEventSequence;
                var snapshots=ResolutionResult.BuildSnapshots(current);var prompts=ResolutionResult.BuildPrompts(current);
                var views=current.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,current.Tick,sequence,snapshots[id],current.Tick,sequence,prompts[id]));
                var spectator=MatchReplayRedactor.BuildSpectatorFrame(current.RoomId,current.Tick,sequence,[],current);
                var errors=MatchRecoveryValidator.Validate(current.RoomId,sequence,commands,events,views,current,current.Tick,spectator);Assert.Empty(errors);
                session=MatchSession.Restore(new(current.RoomId,current.Tick,sequence,commands,events,views,errors,current,initial,spectator),new CoreRuleEngine(),journal);
                coldRestores++;
                entries=journal.Entries.Count;Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted);Assert.Equal(entries,journal.Entries.Count);
            }
        }
        async Task Passes(string prefix) {await Submit(prefix+"-1",current.PriorityPlayerId!,new PassPriorityCommand());await Submit(prefix+"-2",current.PriorityPlayerId!,new PassPriorityCommand());}
        await Submit("enter","P1",new PlayCardCommand("HOST",cardNo,[]));Assert.Single(current.StackItems);Assert.False(current.CardObjects["HOST"].IsEmpowered);
        await Passes("entry-pass");
        await Submit("empower","P1",new ActivateAbilityCommand("HOST","EMPOWER:0:0",[],[]));Assert.False(current.CardObjects["HOST"].IsEmpowered);
        await Passes("empower-pass");Assert.True(current.CardObjects["HOST"].IsEmpowered);
        if(branch=="moonfall")await Submit("move","P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        if(branch=="ability")await Submit("action","P1",OrbOfRegretTests.Command());
        else await Submit("action","P1",new PlayCardCommand("ACTION",branch=="moonfall" ? MoonfallTests.Spell : branch=="counter" ? "OGN·048/298" : "OGN·095/298",branch=="moonfall" ? ["BF","D2"] : branch=="counter" ? [] : ["D"]));
        if(branch is "counter" or "ability" or "control") {
            await Submit("offer-response","P1",new PassPriorityCommand());var item=current.StackItems.Single();
            await Submit("response","P2",new PlayCardCommand("RESPONSE",branch=="counter" ? "UNL-131/219" : branch=="ability" ? "SFD·045/221" : "OGN·080/298",[item.StackItemId]));
            await Passes("response-pass");
            if(branch=="control")await Submit("keep-targets","P2",new PayCostCommand(current.PendingRuleChoice!.Request.Id,"RULE_REPLACEMENT",["KEEP_ALL"]));
            if(current.PendingCardChoice is { } choice)await Submit("predict","P2",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
            Assert.Single(current.StackItems);Assert.Equal(branch=="control" ? "P2" : "P1",current.StackItems.Single().ControllerId);
        }
        await Passes("action-pass");Assert.Empty(current.StackItems);
        if(branch=="moonfall") {Assert.Equal(3,current.CardObjects["D"].Power);Assert.Equal(2,current.CardObjects["D2"].Power);}
        else if(branch!="counter")Assert.Equal(branch=="minimum" ? 1 : branch=="ability" ? 3 : 4,current.CardObjects["D"].Power);
        if(branch=="counter")Assert.Contains("ACTION",current.PlayerZones["P1"].Graveyard);
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,Commands(journal),current,engine,default,Events(journal));Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        Assert.Single(journal.Entries,e=>!e.Accepted);if(native>0)Assert.Equal(steps.Count,native);
        Assert.Equal(branch=="counter" ? 2 : 1,coldRestores);
        Export("steps.json",steps);Export("replay.json",new {nativeCommands=native,accepted=steps.Count,staleRejected=1,coldSessionRestored=true,coldRestores,dedupAfterColdRestore=true,replay});
    }
    private static RecoveredCommand[] Commands(Journal j)=>j.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
    private static RecoveredEvent[] Events(Journal j)=>j.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
    private sealed class Journal:IMatchJournal {public List<MatchJournalEntry> Entries {get;}=[];public ValueTask RecordAsync(MatchJournalEntry e,CancellationToken token) {Entries.Add(e);return ValueTask.CompletedTask;}}
}
