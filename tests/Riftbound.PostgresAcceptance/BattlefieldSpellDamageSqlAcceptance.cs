using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// Current CN OGN296 + OGS002/UNL007 only; no whole-printing or other damage-family certification.
internal static class BattlefieldSpellDamageSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source,string output)
    {
        Check(!Directory.Exists(output),"Fresh bounded battlefield damage SQL output required.");Directory.CreateDirectory(output);
        foreach(var branch in new[]{"targeted-unit","group-void","group-other","group-empty"})await Run(branch);
        async Task Run(string branch)
        {
            var folder=Path.Combine(output,branch);Directory.CreateDirectory(folder);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};var room="current-field-damage-"+Guid.NewGuid().ToString("N");
            var targeted=branch=="targeted-unit";var empty=branch=="group-empty";var spell=targeted?"UNL-007/219":"OGS·002/024";
            string[] selected=targeted?["UNIT"]:[branch=="group-other"?"OTHER":"HILL"];
            var initial=new MatchState(room,0,5,"P1",new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},status:MatchStatuses.InProgress,readyPlayerIds:["P1","P2"],turnPlayerId:"P1",phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
                playerZones:new Dictionary<string,PlayerZones>{{"P1",PlayerZones.Empty with {Hand=["SPELL"],Battlefields=["HILL"],Base=["ALLY"]}},{"P2",PlayerZones.Empty with {Battlefields=empty?["OTHER","OUTSIDE"]:["OTHER","UNIT","SECOND","OUTSIDE"],Base=empty?["UNIT","SECOND"]:[],Hand=["SECRET_HAND"],MainDeck=["SECRET_DECK"]}}},
                cardObjects:new Dictionary<string,CardObjectState>{
                    ["SPELL"]=new("SPELL",cardNo:spell,tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1"),
                    ["HILL"]=new("HILL",cardNo:"OGN·296/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P1",controllerId:empty?null:"P2"),
                    ["OTHER"]=new("OTHER",cardNo:"OGN·275/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P2",controllerId:"P2"),
                    ["UNIT"]=new("UNIT",cardNo:"SFD·069/221",power:8,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2"),
                    ["SECOND"]=new("SECOND",cardNo:"SFD·069/221",power:8,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2"),
                    ["OUTSIDE"]=new("OUTSIDE",cardNo:"SFD·069/221",power:8,tags:[CardObjectTags.UnitCard],ownerId:"P2",controllerId:"P2"),
                    ["ALLY"]=new("ALLY",cardNo:"SFD·069/221",power:8,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1"),
                    ["SECRET_HAND"]=new("SECRET_HAND",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P2",controllerId:"P2"),
                    ["SECRET_DECK"]=new("SECRET_DECK",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P2",controllerId:"P2")},
                objectLocations:new Dictionary<string,ObjectLocationState>{{"SPELL",new("P1","HAND")},{"HILL",new("P1","BATTLEFIELD","HILL")},{"OTHER",new("P2","BATTLEFIELD","OTHER")},{"UNIT",new("P2",empty?"BASE":"BATTLEFIELD",empty?null:"HILL")},{"SECOND",new("P2",empty?"BASE":"BATTLEFIELD",empty?null:"HILL")},{"OUTSIDE",new("P2","BATTLEFIELD","OTHER")},{"ALLY",new("P1","BASE")},{"SECRET_HAND",new("P2","HAND")},{"SECRET_DECK",new("P2","MAIN_DECK")}},
                playerScores:new Dictionary<string,int>{{"P1",0},{"P2",0}},runePools:new Dictionary<string,RunePool>{{"P1",new(20,20)},{"P2",RunePool.Empty}});
            await File.WriteAllTextAsync(Path.Combine(folder,"explicit-original-controlled-initial.json"),JsonSerializer.Serialize(initial,json));
            var engine=new CoreRuleEngine();var journal=new PostgresMatchJournal(source);var players=new PostgresMatchPlayerStore(source);var store=new PostgresMatchRecoveryStore(source);
            foreach(var actor in initial.Seats.Keys)await players.SavePlayerSessionAsync(room,actor,actor,ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),default);
            IMatchSession session=new MatchSession(initial,engine,journal,players);var state=initial;var count=0;var cold=0;var rejected=0;var structural=0;var originals=new List<object>();
            foreach(var targets in new string[][]{[],targeted?["HILL"]:["UNIT"],targeted?["UNIT","SECOND"]:["HILL","OTHER"]})await Reject("P1",new PlayCardCommand("SPELL",spell,targets));
            await Reject("P2",new PlayCardCommand("SPELL",spell,selected));
            await Submit("P1",new PlayCardCommand("SPELL",spell,selected));
            var confirmed=await Save("confirmed-before-damage");var item=state.StackItems.Single();
            Check(item.TargetObjectIds.SequenceEqual(selected)&&item.DamageAmount==3&&item.TargetGenerations is {Count:1}&&item.TargetGenerations[selected[0]]==0,"Literal original target identity, generation and base damage.");
            Check(state.RunePools["P1"].Mana==(targeted?18:14)&&state.RunePools["P1"].Power==19&&state.CardObjects.Values.All(c=>c.Damage==0),"Printed base mana/power paid once, no damage before response passes.");
            foreach(var bad in new[]{item with {TargetObjectIds=targeted?["HILL"]:["UNIT"]},item with {TargetGenerations=new Dictionary<string,long>{{selected[0],-1}}},item with {DamageAmount=99},item with {ControllerId="P2"}})
            {
                Check(MatchRecoveryValidator.Validate(room,confirmed.LastEventSequence,confirmed.Commands,confirmed.Events,confirmed.PlayerViews,state with {StackItems=[bad]},confirmed.CurrentTick,confirmed.SpectatorReplayFrame).Count>0,"Independent selected type/generation/base damage/controller forgery rejected against the original complete recovery frame.");structural++;
            }
            await Submit(state.PriorityPlayerId!,new PassPriorityCommand());await Submit(state.PriorityPlayerId!,new PassPriorityCommand());
            var expectedUnit=targeted||branch=="group-void"?4:0;var expectedSecond=branch=="group-void"?4:0;var expectedOutside=branch=="group-other"?3:0;
            Check(state.CardObjects["UNIT"].Damage==expectedUnit&&state.CardObjects["SECOND"].Damage==expectedSecond&&state.CardObjects["OUTSIDE"].Damage==expectedOutside&&state.CardObjects["ALLY"].Damage==0,"Literal current erratum damage per affected unit; no other battlefield/base/friendly damage.");
            Check(state.StackItems.Count==0&&state.TriggerQueue.Count==0&&state.PendingPayment is null&&state.PendingCardChoice is null&&state.Status==MatchStatuses.InProgress&&state.PlayerScores.Values.All(p=>p==0),"No residual selection, payment, trigger or incidental score.");
            Check(state.PlayerZones["P2"].Hand.SequenceEqual(new[]{"SECRET_HAND"})&&state.PlayerZones["P2"].MainDeck.SequenceEqual(new[]{"SECRET_DECK"})&&state.RngCursor==initial.RngCursor,"Unrelated original hidden hand/deck and RNG unchanged.");
            var final=await Save("final");
            var damageEvents=final.Events.Where(e=>e.Event.Kind=="DAMAGE_APPLIED").ToArray();var expectedIds=targeted?new[]{"UNIT"}:branch=="group-void"?new[]{"SECOND","UNIT"}:branch=="group-other"?new[]{"OUTSIDE"}:Array.Empty<string>();
            Check(damageEvents.Select(e=>JsonSerializer.SerializeToElement(e.Event.Payload,json).GetProperty("targetObjectId").GetString()).Order(StringComparer.Ordinal).SequenceEqual(expectedIds)&&damageEvents.All(e=>JsonSerializer.SerializeToElement(e.Event.Payload,json).GetProperty("damage").GetInt32()==(branch=="group-other"?3:4)),"Literal damage-event affected set and amount.");
            await File.WriteAllTextAsync(Path.Combine(folder,"all-original-accepted-intents.json"),JsonSerializer.Serialize(originals,json));
            await File.WriteAllTextAsync(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{status="REAL_SQL_CURRENT_VOID_ERRATUM_SELECTED_BATTLEFIELD_DAMAGE",room,branch,commands=count,events=final.Events.Count,coldRecoveries=cold,directAtomicRejections=rejected,independentStructuralRejections=structural,actualRuleInputs=state.RuleInputs,wholePrintingOrOtherDamageFamilyCertification=false,explicitOriginalInitialAndAllAcceptedHistoryRetained=true},json));
            Console.WriteLine($"CURRENT_BATTLEFIELD_DAMAGE_SQL_PASS {branch} {count} commands {final.Events.Count} events {cold} cold {rejected} direct {structural} structural");

            async Task Submit(string actor,GameCommand command)
            {
                var intent="current-field-damage-"+count;var node=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();node["promptId"]=session.PromptFor(actor).PromptId;node["snapshotTick"]=state.Tick;var raw=JsonSerializer.SerializeToElement(node,json);
                var result=await session.SubmitAsync(actor,intent,command,raw,default);Check(result.Accepted,result.ErrorMessage??"Original current command rejected.");state=result.State;count++;originals.Add(new{actor,rawCommand=raw});
                var before=await Digest();var frame=await store.LoadAsync(room,default)??throw new Exception("Actual SQL frame required.");Check(frame.IsConsistent&&frame.Commands.Count==count&&Same(frame.AuthoritativeState,state),"Every original authority field and accepted count survive actual JSONB.");
                var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,frame.Commands,state,engine,default,frame.Events);Check(replay.IsMatch,string.Join(';',replay.Errors));var restored=MatchSession.Restore(frame,engine,journal,players);
                foreach(var viewer in initial.Seats.Keys)Check(Same(session.SnapshotFor(viewer),restored.SnapshotFor(viewer))&&Same(session.PromptFor(viewer),restored.PromptFor(viewer)),"Both private full snapshots/prompts/nulls/omissions exact after cold restore.");
                var own=JsonSerializer.Serialize(session.SnapshotFor("P1"),json)+JsonSerializer.Serialize(session.PromptFor("P1"),json);foreach(var id in new[]{"SECRET_HAND","SECRET_DECK"})Check(!own.Contains("\""+id+"\"",StringComparison.Ordinal),"Unrelated opponent hand and deck identities never leak.");
                session=restored;cold++;Check((await session.SubmitAsync(actor,intent,command,raw,default)).Accepted&&await Digest()==before,"Cold duplicate keeps all seven original tables exact.");
            }
            async Task Reject(string actor,GameCommand command)
            {
                var before=await Digest();var result=await engine.ResolveAsync(state,new("invalid-current-field",actor,command.CmdType),command,default);Check(!result.Accepted&&result.Events.Count==0&&Same(result.State,state)&&await Digest()==before,"Missing/excess/wrong-kind target or wrong actor rejects atomically with all SQL rows unchanged.");rejected++;
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
