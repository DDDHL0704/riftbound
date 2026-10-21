using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class RevealedHandPromptOrderSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source,string output)
    {
        Check(!Directory.Exists(output),"Fresh bounded hand-prompt SQL output required.");Directory.CreateDirectory(output);
        foreach(var branch in new[]{"owner-play","decline"})await Run(branch);
        async Task Run(string branch)
        {
            var folder=Path.Combine(output,branch);Directory.CreateDirectory(folder);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};var room="revealed-hand-order-"+Guid.NewGuid().ToString("N");
            var initial=new MatchState(room,0,5,"P1",new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},status:MatchStatuses.InProgress,readyPlayerIds:["P1","P2"],turnPlayerId:"P1",phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
                playerZones:new Dictionary<string,PlayerZones>{{"P1",PlayerZones.Empty with {Hand=["C"],Battlefields=["BF"]}},{"P2",PlayerZones.Empty with {Hand=["U","ALTERNATE_UNIT","SPELL"],MainDeck=["HIDDEN_DECK"],Base=["ALLY"]}}},
                cardObjects:new Dictionary<string,CardObjectState>{
                    ["C"]=new("C",cardNo:"UNL-139/219",manaCost:2,tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1"),
                    ["BF"]=new("BF",cardNo:"OGN·296/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P1",controllerId:"P1"),
                    ["U"]=new("U",cardNo:"OGN·208/298",power:6,manaCost:4,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2"),
                    ["ALTERNATE_UNIT"]=new("ALTERNATE_UNIT",cardNo:"OGN·096/298",power:1,manaCost:2,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2"),
                    ["SPELL"]=new("SPELL",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P2",controllerId:"P2"),
                    ["HIDDEN_DECK"]=new("HIDDEN_DECK",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P2",controllerId:"P2"),
                    ["ALLY"]=new("ALLY",cardNo:"OGN·096/298",power:1,manaCost:2,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2")},
                objectLocations:new Dictionary<string,ObjectLocationState>{{"C",new("P1","HAND")},{"BF",new("P1","BATTLEFIELD","BF")},{"U",new("P2","HAND")},{"ALTERNATE_UNIT",new("P2","HAND")},{"SPELL",new("P2","HAND")},{"HIDDEN_DECK",new("P2","MAIN_DECK")},{"ALLY",new("P2","BASE")}},
                playerScores:new Dictionary<string,int>{{"P1",0},{"P2",0}},runePools:new Dictionary<string,RunePool>{{"P1",new(2,2)},{"P2",RunePool.Empty}});
            await File.WriteAllTextAsync(Path.Combine(folder,"explicit-original-controlled-initial.json"),JsonSerializer.Serialize(initial,json));
            var engine=new CoreRuleEngine();var journal=new PostgresMatchJournal(source);var players=new PostgresMatchPlayerStore(source);var store=new PostgresMatchRecoveryStore(source);
            foreach(var actor in initial.Seats.Keys)await players.SavePlayerSessionAsync(room,actor,actor,ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),default);
            IMatchSession session=new MatchSession(initial,engine,journal,players);var state=initial;var count=0;var cold=0;var rejected=0;var structural=0;var originals=new List<object>();
            await Submit("P1",new PlayCardCommand("C","UNL-139/219",["BF"]));
            await Save("before-reveal");
            Check(!JsonSerializer.Serialize(session.SnapshotFor("P1"),json).Contains("\"SPELL\"",StringComparison.Ordinal),"Opponent hand remains hidden before original spell resolution.");
            await Submit(state.PriorityPlayerId!,new PassPriorityCommand());await Submit(state.PriorityPlayerId!,new PassPriorityCommand());
            var pending=state.PendingEffectPlay??throw new Exception("Actual revealed-hand selection required.");
            Check(pending.RevealedHand is {OwnerId:"P2",Choosing:true,BattlefieldId:"BF"}&&pending.PlayerId=="P1"&&pending.IgnoreAllCosts&&pending.Optional,"Exact original choosing actor, owner, field and all-cost waiver.");
            Check(pending.Sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(new[]{"ALTERNATE_UNIT","U"}),"Only the two original units are legal sources; spell and hidden deck are excluded.");
            var prompt=JsonSerializer.SerializeToNode(session.PromptFor("P1"),json)!;
            var candidate=prompt["candidates"]!.AsArray().Single(n=>n!["action"]!.GetValue<string>()==CommandTypes.ChooseCards)!;
            Check(candidate["metadata"]!["viewedCards"]!.AsArray().Select(n=>n!["id"]!.GetValue<string>()).SequenceEqual(new[]{"ALTERNATE_UNIT","SPELL","U"}),"Stable ordinal presentation with different-length keys in the actual JSONB-cold prompt.");
            var revealed=await Save("revealed-choice");
            foreach(var mutation in new[]{"actor","generation","free-cost","destination"})
            {
                var bad=mutation switch {"actor"=>pending with {PlayerId="P2"},"generation"=>pending with {Sources=new Dictionary<string,long>{{"U",99}}},"free-cost"=>pending with {IgnoreAllCosts=false},_=>pending with {DestinationPolicy="ANY_BATTLEFIELD"}};
                Check(MatchRecoveryValidator.Validate(room,revealed.LastEventSequence,revealed.Commands,revealed.Events,revealed.PlayerViews,state with {PendingEffectPlay=bad},revealed.CurrentTick).Count>0,"Independent revealed-hand permission forgery rejected: "+mutation);structural++;
            }
            await Reject("P2",new ChooseCardsCommand(pending.ChoiceId,"REVEALED_HAND_PLAY",["U"]));
            await Reject("P1",new ChooseCardsCommand("stale-"+pending.ChoiceId,"REVEALED_HAND_PLAY",["U"]));
            foreach(var id in new[]{"SPELL","HIDDEN_DECK"})await Reject("P1",new ChooseCardsCommand(pending.ChoiceId,"REVEALED_HAND_PLAY",[id]));
            await Reject("P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
            await Submit("P1",new ChooseCardsCommand(pending.ChoiceId,"REVEALED_HAND_PLAY",branch=="owner-play"?["U"]:[]));
            if(branch=="owner-play")
            {
                pending=state.PendingEffectPlay??throw new Exception("Original owner must confirm selected unit.");
                Check(pending.PlayerId=="P2"&&!pending.Optional&&pending.Sources.Keys.SequenceEqual(new[]{"U"})&&pending.RevealedHand is {Choosing:false},"Selection transfers only the original unit's confirmation to its owner.");
                await Save("owner-confirmation");
                await Reject("P1",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
                await Reject("P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BASE"));
                await Reject("P2",new PlayCardCommand("ALTERNATE_UNIT","OGN·096/298",[],Destination:"BATTLEFIELD:BF"));
                await Reject("P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF",OptionalCosts:["DESTROY_FRIENDLY_UNIT:ALLY"]));
                await Submit("P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
                Check(state.PlayerZones["P2"].Battlefields.Contains("U")&&state.ObjectLocations["U"].BattlefieldObjectId=="BF"&&state.CardObjects["U"].UntilEndOfTurnEffects.Contains("STUNNED"),"Original selected unit actually plays to the fixed field and is stunned.");
            }
            Check(state.PendingEffectPlay is null&&state.PlayerZones["P2"].Hand.SequenceEqual(branch=="owner-play"?new[]{"ALTERNATE_UNIT","SPELL"}:new[]{"U","ALTERNATE_UNIT","SPELL"})&&state.PlayerZones["P2"].Base.SequenceEqual(new[]{"ALLY"})&&state.RunePools["P2"]==RunePool.Empty,"Unselected original hand, additional-sacrifice unit and all owner's resources remain exact.");
            var other=JsonSerializer.Serialize(session.SnapshotFor("P1"),json)+JsonSerializer.Serialize(session.PromptFor("P1"),json);
            foreach(var id in state.PlayerZones["P2"].Hand.Append("HIDDEN_DECK"))Check(!other.Contains("\""+id+"\"",StringComparison.Ordinal),"Unselected hand and unrelated deck return to private projection.");
            var final=await Save("final");
            Check(final.Events.Count(e=>e.Event.Kind=="HAND_REVEALED")==1&&final.Events.Count(e=>e.Event.Kind=="STATUS_EFFECT_APPLIED")== (branch=="owner-play"?1:0)&&final.Events.All(e=>e.Event.Kind!="UNIT_DESTROYED"),"One original reveal, only successful play gets stun, and no additional sacrifice.");
            await File.WriteAllTextAsync(Path.Combine(folder,"all-original-accepted-intents.json"),JsonSerializer.Serialize(originals,json));
            await File.WriteAllTextAsync(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{status="REAL_SQL_REVEALED_HAND_STABLE_PROMPT_ORIGINAL_SELECTION_OWNER_PLAY_AND_COLD_REPLAY_PASS",room,branch,commands=count,events=final.Events.Count,coldRecoveries=cold,directAtomicRejections=rejected,independentStructuralRejections=structural,actualRuleInputs=state.RuleInputs,explicitControlledInitialOnly=true,oldBlankReplayMetadataNotRewritten=true,wholePrintingVisibleWindowsOrNormalOpeningCertification=false},json));
            Console.WriteLine($"REVEALED_HAND_PROMPT_SQL_PASS {branch} {count} commands {final.Events.Count} events {cold} cold {rejected} direct {structural} structural");
            async Task Submit(string actor,GameCommand command)
            {
                var intent="hand-order-"+count;var node=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();node["promptId"]=session.PromptFor(actor).PromptId;node["snapshotTick"]=state.Tick;var raw=JsonSerializer.SerializeToElement(node,json);
                var result=await session.SubmitAsync(actor,intent,command,raw,default);Check(result.Accepted,result.ErrorMessage??"Original hand command rejected.");state=result.State;count++;originals.Add(new{actor,rawCommand=raw});
                var before=await Digest();var frame=await store.LoadAsync(room,default)??throw new Exception("Actual SQL frame required.");Check(frame.IsConsistent&&frame.Commands.Count==count&&Same(frame.AuthoritativeState,state),"Every original authority field and count survives actual JSONB recovery.");
                var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,frame.Commands,state,engine,default,frame.Events);Check(replay.IsMatch,string.Join(';',replay.Errors));var restored=MatchSession.Restore(frame,engine,journal,players);
                foreach(var viewer in initial.Seats.Keys)Check(Same(session.SnapshotFor(viewer),restored.SnapshotFor(viewer))&&Same(session.PromptFor(viewer),restored.PromptFor(viewer)),"Every private snapshot and prompt field/null/omission remains exact after cold JSONB.");
                session=restored;cold++;Check((await session.SubmitAsync(actor,intent,command,raw,default)).Accepted&&await Digest()==before,"Cold duplicate leaves all seven original tables unchanged.");
            }
            async Task Reject(string actor,GameCommand command)
            {
                var before=await Digest();var result=await engine.ResolveAsync(state,new("invalid-hand-order",actor,command.CmdType),command,default);Check(!result.Accepted&&result.Events.Count==0&&Same(result.State,state)&&await Digest()==before,"Wrong actor/source/cost/destination/stale command rejects with no state/events/SQL mutation.");rejected++;
            }
            async Task<MatchRecoveryFrame> Save(string phase)
            {
                var frame=await store.LoadAsync(room,default)??throw new Exception("Actual SQL checkpoint required.");await using var file=new FileStream(Path.Combine(folder,phase+"-original-frame.json.gz"),FileMode.CreateNew);await using var zip=new GZipStream(file,CompressionLevel.SmallestSize);await JsonSerializer.SerializeAsync(zip,frame,json);return frame;
            }
            async Task<string> Digest()
            {
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);await using var connection=await source.OpenConnectionAsync();
                foreach(var table in new[]{"matches","match_players","command_log","game_events","state_snapshots","snapshots","action_prompts"})
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(table+"\n"));await using var query=new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text",connection);query.Parameters.AddWithValue("room",room);await using var rows=await query.ExecuteReaderAsync();while(await rows.ReadAsync())hash.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0)+"\n"));
                }
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            bool Same<T>(T a,T b)=>JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a,json),JsonSerializer.SerializeToNode(b,json));
        }
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
