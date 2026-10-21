using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// Current CN OGN·291/298 with exhausted SFD·187/221 and SFD·243/221, both confirmation orders; no whole-printing certification.
internal static class ExhaustedLegendConfirmationSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source,string output)
    {
        Check(!Directory.Exists(output),"Fresh exhausted-legend confirmation SQL output required.");
        Directory.CreateDirectory(output);
        foreach(var printing in new[]{"SFD\u00b7187/221","SFD\u00b7243/221"})foreach(var branch in new[]{"legend-first","battlefield-first"})await Run(printing,branch);
        async Task Run(string printing,string branch)
        {
            var directory=Path.Combine(output,printing.Contains("187")?"sfd187":"sfd243",branch);Directory.CreateDirectory(directory);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
            var room="exhausted-legend-pending-"+Guid.NewGuid().ToString("N");var count=3;
            var cards=new Dictionary<string,CardObjectState>();
            foreach(var id in new[]{"DRAW1","DRAW2"})cards[id]=new(id,cardNo:"SFD·125/221",power:4,manaCost:4,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1");
            cards["DRAW3"]=new("DRAW3",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1");
            cards["LEGEND"]=new("LEGEND",cardNo:printing,tags:["CARD_TYPE:LEGEND"],ownerId:"P1",controllerId:"P1",isExhausted:true);
            cards["BF"]=new("BF",cardNo:"OGN·291/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P2");
            cards["UNIT"]=new("UNIT",cardNo:"OGN·096/298",power:1,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1");
            cards["RUNE"]=new("RUNE",cardNo:"OGN·007/298",tags:[CardObjectTags.RuneCard],ownerId:"P1",controllerId:"P1",isExhausted:true);cards["RUNE2"]=cards["RUNE"] with {ObjectId="RUNE2"};
            var deck=new[]{"DRAW1","DRAW2","DRAW3"};
            var initial=new MatchState(room,0,5,"P1",new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},status:MatchStatuses.InProgress,readyPlayerIds:["P1","P2"],turnPlayerId:"P1",phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
                playerZones:new Dictionary<string,PlayerZones>{["P1"]=PlayerZones.Empty with {Base=["UNIT","RUNE","RUNE2"],LegendZone=["LEGEND"],Hand=deck.Skip(count).ToArray(),MainDeck=deck.Take(count).ToArray()},["P2"]=PlayerZones.Empty with {Battlefields=["BF"]}},
                cardObjects:cards,playerScores:new Dictionary<string,int>{{"P1",0},{"P2",0}},runePools:new Dictionary<string,RunePool>{{"P1",new(20,10)},{"P2",RunePool.Empty}});
            await File.WriteAllTextAsync(Path.Combine(directory,"explicit-original-controlled-initial.json"),JsonSerializer.Serialize(initial,json));
            var engine=new CoreRuleEngine();var journal=new PostgresMatchJournal(source);var players=new PostgresMatchPlayerStore(source);var store=new PostgresMatchRecoveryStore(source);
            foreach(var actor in initial.Seats.Keys)await players.SavePlayerSessionAsync(room,actor,actor,ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),default);
            IMatchSession session=new MatchSession(initial,engine,journal,players);var state=initial;var commands=0;var cold=0;var rejected=0;var structural=0;var originals=new List<object>();
            await Submit("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
            while(state.FocusPlayerId is not null)await Submit(state.FocusPlayerId,new PassFocusCommand());
            Check(state.PlayerScores["P1"]==1&&state.PlayerScores["P2"]==0,"Literal normal conquest point.");
            Check(state.TriggerQueue.Count==2&&state.TriggerQueue.Select(t=>t.SourceObjectId).Order().SequenceEqual(new[]{"BF","LEGEND"})&&state.CardObjects["LEGEND"].IsExhausted,"Both actual conquest triggers are captured even while the legend is already exhausted; no look or cost occurs at capture.");
            var captured=await Save("before-order");var legend=state.TriggerQueue.Single(t=>t.SourceObjectId=="LEGEND");
            foreach(var bad in new[]{legend with {LegendConquest=legend.LegendConquest! with {SourceGeneration=-1}},legend with {LegendConquest=legend.LegendConquest! with {Kind="PAY_READY_SELF"}},legend with {EffectKind="SPELL"},legend with {TriggeredByEventKind="UNIT_PLAYED"}})
            {
                var forged=state with {TriggerQueue=state.TriggerQueue.Select(t=>t.SourceObjectId=="LEGEND"?bad:t).ToArray()};
                Check(MatchRecoveryValidator.Validate(room,captured.LastEventSequence,captured.Commands,captured.Events,captured.PlayerViews,forged,captured.CurrentTick,captured.SpectatorReplayFrame).Count>0,"Independent malformed generation, definition, effect or captured-event forgery rejected.");structural++;
            }
            var ids=state.TriggerQueue.OrderBy(t=>t.SourceObjectId==(branch=="legend-first"?"LEGEND":"BF")?0:1).Select(t=>t.TriggerId).ToArray();
            await Reject("P2",new OrderTriggersCommand(OrderedTriggerIds:ids));
            await Reject("P1",new OrderTriggersCommand(OrderedTriggerIds:[ids[0]]));
            await Reject("P1",new OrderTriggersCommand(OrderedTriggerIds:[ids[0],ids[0]]));
            await Submit("P1",new OrderTriggersCommand(OrderedTriggerIds:ids));
            Check(state.CardObjects["LEGEND"].IsExhausted&&state.TriggerQueue.Count==0&&state.StackItems.Count==1&&state.StackItems[0].SourceObjectId=="BF"&&state.StackItems[0].TriggerCost is null&&state.PendingCardChoice is null&&state.PendingPayment is null&&state.PriorityPlayerId=="P1","Already exhausted legend fails while confirming and is removed; the other original ability remains independently respondable.");
            Check(state.RunePools["P1"].Mana==20&&state.RunePools["P1"].Power==10&&state.PlayerZones["P1"].MainDeck.SequenceEqual(new[]{"DRAW1","DRAW2","DRAW3"}),"Failed source-exhaust cost spends nothing and creates no premature reveal or deck action.");
            await Save("failed-legend-cost-before-battlefield-look");
            await Reject("P1",new ChooseCardsCommand("TRIGGER-COST:LEGEND","TRIGGER_COST_CONFIRMATION",["LEGEND"]));
            await Reject("P1",new PlayCardCommand("DRAW1","SFD·125/221",[]));
            await Submit(state.PriorityPlayerId!,new PassPriorityCommand());await Submit(state.PriorityPlayerId!,new PassPriorityCommand());
            var choice=state.PendingCardChoice??throw new Exception("Only the original private battlefield look should resolve.");
            Check(choice.ChoiceWindow=="INSIGHT"&&choice.PlayerId=="P1"&&choice.RequiredCount==0&&choice.MaxCount==2&&choice.LegalObjectIds.SequenceEqual(new[]{"DRAW1","DRAW2"}),"Failed legendary trigger does not consume the original battlefield top pair.");
            await Save("private-look");
            await Reject("P2",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
            await Reject("P1",new ChooseCardsCommand("stale-"+choice.ChoiceId,choice.ChoiceWindow,[]));
            await Submit("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
            choice=state.PendingCardChoice!;Check(choice.ChoiceWindow=="INSIGHT_ORDER"&&choice.LegalObjectIds.SequenceEqual(new[]{"DRAW1","DRAW2"}),"Only the same retained original private pair can be reordered.");
            await Reject("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["DRAW1","DRAW1"]));
            await Submit("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["DRAW2","DRAW1"]));
            var final=await Save("final");
            Check(state.Status==MatchStatuses.InProgress&&state.PlayerScores["P1"]==1&&state.PlayerScores["P2"]==0&&state.PendingCardChoice is null&&state.PendingEffectPlay is null&&state.PendingPayment is null&&state.StackItems.Count==0&&state.TriggerQueue.Count==0,"Exactly the original conquest and no residual failed-cost or play windows.");
            Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(new[]{"DRAW2","DRAW1","DRAW3"})&&state.CardObjects["LEGEND"].IsExhausted&&state.RunePools["P1"].Mana==20&&state.RunePools["P1"].Power==10&&state.RngCursor==initial.RngCursor,"The private original pair reorders while source exhaustion, resources, remainder and RNG remain exact.");
            Check(final.Events.Count(e=>e.Event.Kind=="TRIGGER_NOT_CONFIRMED")==1&&final.Events.Where(e=>e.Event.Kind=="TRIGGER_NOT_CONFIRMED").All(e=>JsonSerializer.SerializeToElement(e.Event.Payload,json).GetProperty("sourceObjectId").GetString()=="LEGEND")&&final.Events.All(e=>e.Event.Kind is not("CARDS_REVEALED" or "CARD_DRAWN" or "MAIN_DECK_BURNED_OUT")),"Original exhausted legendary trigger is removed exactly once during confirmation without any public reveal, draw or burnout.");
            await File.WriteAllTextAsync(Path.Combine(directory,"all-original-accepted-intents.json"),JsonSerializer.Serialize(originals,json));
            await File.WriteAllTextAsync(Path.Combine(directory,"receipt.json"),JsonSerializer.Serialize(new{status="REAL_SQL_CURRENT_EXHAUSTED_REKSAI_CAPTURE_CONFIRMATION_FAILURE_CANDLELIT_ONLY",room,printing,branch,commands,events=final.Events.Count,coldRecoveries=cold,directAtomicRejections=rejected,independentStructuralRejections=structural,actualRuleInputs=state.RuleInputs,rootIntegration=true,wholePrintingCertification=false,explicitOriginalInitialAndAllAcceptedHistoryRetained=true},json));
            Console.WriteLine($"EXHAUSTED_REKSAI_CONFIRMATION_SQL_PASS {printing} {branch} {commands} commands {final.Events.Count} events {cold} cold {rejected} direct {structural} structural");

            async Task Submit(string actor,GameCommand command)
            {
                var intent="ordered-legend-"+commands;var raw=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();raw["promptId"]=session.PromptFor(actor).PromptId;raw["snapshotTick"]=state.Tick;var element=JsonSerializer.SerializeToElement(raw,json);
                var accepted=await session.SubmitAsync(actor,intent,command,element,default);Check(accepted.Accepted,accepted.ErrorMessage??"Original command rejected.");state=accepted.State;commands++;originals.Add(new{actor,rawCommand=element});
                var before=await Digest();var frame=await store.LoadAsync(room,default)??throw new Exception("Actual SQL frame required.");Check(frame.IsConsistent&&frame.Commands.Count==commands&&Same(frame.AuthoritativeState,state),"Whole original SQL state and current accepted command count.");
                var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,frame.Commands,state,engine,default,frame.Events);Check(replay.IsMatch,string.Join(';',replay.Errors));
                var restored=MatchSession.Restore(frame,engine,journal,players);
                foreach(var viewer in initial.Seats.Keys)Check(Same(session.SnapshotFor(viewer),restored.SnapshotFor(viewer))&&Same(session.PromptFor(viewer),restored.PromptFor(viewer)),"Cold private snapshot and prompt fields/nulls are exact.");
                session=restored;cold++;Check((await session.SubmitAsync(actor,intent,command,element,default)).Accepted&&await Digest()==before,"Cold duplicate leaves all seven original tables unchanged.");
            }
            async Task Reject(string actor,GameCommand command)
            {
                var before=await Digest();var result=await engine.ResolveAsync(state,new("invalid-ordered-legend",actor,command.CmdType),command,default);
                Check(!result.Accepted&&result.Events.Count==0&&Same(result.State,state)&&await Digest()==before,"Wrong/stale/forged private selection rejects atomically with the full original state and all seven SQL tables unchanged.");rejected++;
            }
            async Task<MatchRecoveryFrame> Save(string phase)
            {
                var frame=await store.LoadAsync(room,default)??throw new Exception("Real SQL frame required.");await using var file=new FileStream(Path.Combine(directory,phase+"-original-frame.json.gz"),FileMode.CreateNew);await using var zip=new GZipStream(file,CompressionLevel.SmallestSize);await JsonSerializer.SerializeAsync(zip,frame,json);return frame;
            }
            async Task<string> Digest()
            {
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);await using var connection=await source.OpenConnectionAsync();
                foreach(var table in new[]{"matches","match_players","command_log","game_events","state_snapshots","snapshots","action_prompts"})
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(table+"\n"));await using var q=new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text",connection);q.Parameters.AddWithValue("room",room);await using var rows=await q.ExecuteReaderAsync();while(await rows.ReadAsync())hash.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0)+"\n"));
                }
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            bool Same<T>(T left,T right)=>JsonNode.DeepEquals(JsonSerializer.SerializeToNode(left,json),JsonSerializer.SerializeToNode(right,json));
        }
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
