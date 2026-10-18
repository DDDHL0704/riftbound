using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
var folder=Path.GetFullPath(args.Single());Check(!Directory.Exists(folder),"Never overwrite original projections.");Directory.CreateDirectory(folder);
var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
using var doc=JsonDocument.Parse(await File.ReadAllTextAsync("tests/Riftbound.ConformanceTests/Fixtures/empty-observed-deck-repeat-openings.json"));
var index=new List<object>();var summaries=new List<object>();
foreach(var recipe in doc.RootElement.GetProperty("branches").EnumerateArray())
{
 var card=recipe.GetProperty("cardNo").GetString()!;var branch=card.StartsWith("SFD")?"sfd":"unl";
 var first=recipe.GetProperty("expectedFirstView").Deserialize<string[]>()!;var second=recipe.GetProperty("expectedSecondView").Deserialize<string[]>()!;
 var initial=MatchReplayInitialStateBuilder.FromSeats("native-observed-presentation-"+branch,new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}});
 Check(initial.Tick==0 && initial.Seed==0 && initial.CardObjects.Count==0,"Original ordinary blank seed0.");
 var engine=new CoreRuleEngine();var journal=new Journal();IMatchSession session=new MatchSession(initial,engine,journal);var state=initial;var banishes=0;
 foreach(var entry in recipe.GetProperty("acceptedCommands").EnumerateArray())
 {
  if(state.PendingRuleChoice is {Request.Kind:"DECK_OBSERVATION_BANISH"})Export("banish",banishes++);
  await Submit(entry.GetProperty("playerId").GetString()!,GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
 }
 Check(banishes==first.Length,"Export every actual private observed-banish decision.");
 var choice=state.PendingCardChoice!;Check(choice.ChoiceWindow=="DECK_EFFECT" && choice.DeckContext!.ExecutionIndex==1,"Actual second view.");
 await Submit("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[second[1]]));
 Check(state.PendingEffectPlay is not null,"Actual observed play offer.");
 if(state.RunePools["P1"].Power+state.RunePools["P1"].PowerByTrait.Values.Sum()==0)
  await Submit("P1",new RecycleRuneCommand(state.PlayerZones["P1"].Base.First(id=>state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard))));
 var candidate=session.PromptFor("P1").Candidates!.Single(c=>c.Action==CommandTypes.PlayCard);
 var metadata=JsonSerializer.SerializeToElement(candidate.Metadata,json);var source=metadata.GetProperty("sourceRequirements").EnumerateArray().Single();
 Check(source.GetProperty("sourceZone").GetString()=="STACK" && source.GetProperty("cardNo").GetString()=="OGN·194/298","Actual server-authorized observed Nocturne STACK source.");
 Export("preview",0);
 var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage){RuleInputs=e.RuleInputs}).ToArray();
 var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
 var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,state,engine,default,events);Check(replay.IsMatch,string.Join(";",replay.Errors));
 summaries.Add(new {branch,card,commands=commands.Length,events=events.Length,originalBlankSeed=0,banishDecisions=banishes,originalReplayMatched=true,ruleInputs=state.RuleInputs});
 async Task Submit(string player,GameCommand command)
 {
  var fields=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();fields["promptId"]=session.PromptFor(player).PromptId;fields["snapshotTick"]=state.Tick;var raw=JsonSerializer.SerializeToElement(fields,json);var id="native-observed-ui-"+journal.Entries.Count;
  var result=command switch{SubmitDeckCommand deck=>await session.SubmitDeckAsync(player,id,deck,raw,default),ReadyCommand=>await session.ReadyAsync(player,id,raw,default),_=>await session.SubmitAsync(player,id,command,raw,default)};
  Check(result.Accepted,result.ErrorMessage??"Command rejected");state=result.State;
 }
 void Export(string kind,int number)
 {
  var name=branch+"-"+kind+"-"+number;var target=Path.Combine(folder,name);Directory.CreateDirectory(target);
  foreach(var player in new[]{"P1","P2"})
  {
   File.WriteAllText(Path.Combine(target,player+"-prompt.json"),JsonSerializer.Serialize(session.PromptFor(player),json));
   File.WriteAllText(Path.Combine(target,player+"-snapshot.json"),JsonSerializer.Serialize(session.SnapshotFor(player),json));
  }
  index.Add(new {name,kind,cardNo=card,owner="P1",firstIds=first,sourceId=kind=="preview"?state.PendingEffectPlay!.Sources.Keys.Single():null,expectedCardNo="OGN·194/298",notLiveNativeAcceptance=true});
 }
}
await File.WriteAllTextAsync(Path.Combine(folder,"index.json"),JsonSerializer.Serialize(index,json));
await File.WriteAllTextAsync(Path.Combine(folder,"ordinary-opening-verification.json"),JsonSerializer.Serialize(summaries,json));
Console.WriteLine("Actual ordinary projections exported; original full action/event replay matches; no database or live-client command submitted.");
static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
sealed class Journal:IMatchJournal{public List<MatchJournalEntry> Entries{get;}=[];public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken cancellationToken){Entries.Add(entry);return ValueTask.CompletedTask;}}
