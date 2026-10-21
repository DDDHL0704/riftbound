using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// CN OGN·291/298: bounded controlled conquest LOOK cases, not printed Insight or whole-card certification.
internal static class CandlelitPrivateLookSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source,string output)
    {
        Check(!Directory.Exists(output),"Never overwrite an original current Candlelit SQL run.");
        Directory.CreateDirectory(output);
        foreach(var branch in new[]{"keep-reverse","recycle-one","recycle-two","short-empty","short-one"})await Run(branch);
        async Task Run(string branch)
        {
            var directory=Path.Combine(output,branch);Directory.CreateDirectory(directory);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
            var room="candlelit-private-look-"+Guid.NewGuid().ToString("N");var count=branch=="short-empty"?0:branch=="short-one"?1:3;
            var cards=new Dictionary<string,CardObjectState>();
            foreach(var id in new[]{"DRAW1","DRAW2","DRAW3"})cards[id]=new(id,cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1");
            cards["BF"]=new("BF",cardNo:"OGN·291/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P2");
            cards["UNIT"]=new("UNIT",cardNo:"OGN·096/298",power:1,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1");
            cards["RUNE"]=new("RUNE",cardNo:"OGN·007/298",tags:[CardObjectTags.RuneCard],ownerId:"P1",controllerId:"P1",isExhausted:true);cards["RUNE2"]=cards["RUNE"] with {ObjectId="RUNE2"};
            var deck=new[]{"DRAW1","DRAW2","DRAW3"};
            var initial=new MatchState(room,0,5,"P1",new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},status:MatchStatuses.InProgress,readyPlayerIds:["P1","P2"],turnPlayerId:"P1",phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
                playerZones:new Dictionary<string,PlayerZones>{["P1"]=PlayerZones.Empty with {Base=["UNIT","RUNE","RUNE2"],Hand=deck.Skip(count).ToArray(),MainDeck=deck.Take(count).ToArray()},["P2"]=PlayerZones.Empty with {Battlefields=["BF"]}},
                cardObjects:cards,playerScores:new Dictionary<string,int>{{"P1",0},{"P2",0}},runePools:new Dictionary<string,RunePool>{{"P1",new(10,10)},{"P2",RunePool.Empty}});
            await File.WriteAllTextAsync(Path.Combine(directory,"explicit-original-controlled-initial.json"),JsonSerializer.Serialize(initial,json));
            var engine=new CoreRuleEngine();var journal=new PostgresMatchJournal(source);var players=new PostgresMatchPlayerStore(source);var store=new PostgresMatchRecoveryStore(source);
            foreach(var actor in initial.Seats.Keys)await players.SavePlayerSessionAsync(room,actor,actor,ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),default);
            IMatchSession session=new MatchSession(initial,engine,journal,players);var state=initial;var commands=0;var cold=0;var rejected=0;var structural=0;var originals=new List<object>();
            await Submit("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
            while(state.FocusPlayerId is not null)await Submit(state.FocusPlayerId,new PassFocusCommand());
            Check(state.PlayerScores["P1"]==1&&state.PlayerScores["P2"]==0,"Literal normal conquest point.");
            var captured=state.StackItems.Single();Check(captured.InsightContext is {Kind:"CONQUEST_LOOK",Count:2}&&captured.ControllerId=="P1"&&captured.SourceObjectId=="BF"&&state.CardObjects["BF"].OwnerId=="P2","Mandatory captured source belongs to the conqueror, not the printed battlefield owner.");
            Check(state.PendingCardChoice is null&&state.PlayerZones["P1"].MainDeck.SequenceEqual(initial.PlayerZones["P1"].MainDeck),"No early look/recycle before ordinary response.");
            await Save("before-response");
            var genuine=await store.LoadAsync(room,default)??throw new Exception("Real frame required.");
            Check(MatchRecoveryValidator.Validate(room,genuine.LastEventSequence,genuine.Commands,genuine.Events,genuine.PlayerViews,state,genuine.CurrentTick).Count==0,"Independent structural validator accepts the genuine full frame before mutation.");
            foreach(var mutation in new[]{"count","source","battlefield","generation"})
            {
                var context=captured.InsightContext!;var altered=captured;
                if(mutation=="source")altered=altered with {SourceObjectId="UNIT"};
                else context=mutation switch {"count"=>context with {Count=99},"battlefield"=>context with {BattlefieldObjectId="UNIT"},_=>context with {SourceGeneration=99}};
                var forged=state with {StackItems=[altered with {InsightContext=context}]};
                Check(MatchRecoveryValidator.Validate(room,genuine.LastEventSequence,genuine.Commands,genuine.Events,genuine.PlayerViews,forged,genuine.CurrentTick).Count>0,"Independent captured-source mutation rejected: "+mutation);structural++;
            }
            await Submit(state.PriorityPlayerId!,new PassPriorityCommand());await Submit(state.PriorityPlayerId!,new PassPriorityCommand());
            if(count==0)Check(state.PendingCardChoice is null&&state.StackItems.Count==0,"Empty private look finishes without impossible choice.");
            else
            {
                var choice=state.PendingCardChoice!;var viewed=deck.Take(Math.Min(2,count)).ToArray();
                Check(choice.ChoiceWindow=="INSIGHT"&&choice.PlayerId=="P1"&&choice.RequiredCount==0&&choice.MaxCount==viewed.Length&&choice.LegalObjectIds.SequenceEqual(viewed),"Only existing original top cards; literal zero-to-all subset.");
                var opponent=JsonSerializer.Serialize(session.SnapshotFor("P2"),json)+JsonSerializer.Serialize(session.PromptFor("P2"),json);
                foreach(var id in viewed)Check(!opponent.Contains("\""+id+"\"",StringComparison.Ordinal),"Private look not exposed to original battlefield owner.");
                await Save("private-subset");
                await Reject("P2",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
                await Reject("P1",new ChooseCardsCommand("stale-"+choice.ChoiceId,choice.ChoiceWindow,[]));
                await Reject("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["RUNE"]));
                await Reject("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[viewed[0],viewed[0]]));
                await Reject("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["DRAW1","DRAW2","DRAW3"]));
                var selection=branch=="recycle-one"?new[]{"DRAW1"}:branch=="recycle-two"?new[]{"DRAW1","DRAW2"}:[];
                await Submit("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,selection));
                if(state.PendingCardChoice is { } order)
                {
                    Check(order.ChoiceWindow=="INSIGHT_ORDER"&&order.RequiredCount==2&&order.MaxCount==2&&order.LegalObjectIds.SequenceEqual(viewed),"Only retained two originals can be ordered.");
                    await Reject("P1",new ChooseCardsCommand(order.ChoiceId,order.ChoiceWindow,["DRAW1"]));
                    await Save("private-order");await Submit("P1",new ChooseCardsCommand(order.ChoiceId,order.ChoiceWindow,["DRAW2","DRAW1"]));
                }
            }
            var final=await Save("final");var resultDeck=state.PlayerZones["P1"].MainDeck;
            Check(state.Status==MatchStatuses.InProgress&&state.PlayerScores["P1"]==1&&state.PlayerScores["P2"]==0&&state.PendingCardChoice is null&&state.StackItems.Count==0,"One original conquest; no draw, burnout, extra score or residual window.");
            if(branch=="keep-reverse")Check(resultDeck.SequenceEqual(new[]{"DRAW2","DRAW1","DRAW3"})&&state.RngCursor==initial.RngCursor,"Literal chosen reverse top order; no randomization for zero recycle.");
            if(branch=="recycle-one")Check(resultDeck.SequenceEqual(new[]{"DRAW2","DRAW3","DRAW1"})&&state.RngCursor==initial.RngCursor,"One exact original card recycled to bottom; retained top is exact.");
            if(branch=="recycle-two")Check(resultDeck[0]=="DRAW3"&&resultDeck.Skip(1).Order().SequenceEqual(new[]{"DRAW1","DRAW2"})&&state.RngCursor==initial.RngCursor+1,"Exactly selected bottom pair randomizes once with original seed.");
            if(branch.StartsWith("short-",StringComparison.Ordinal))Check(resultDeck.SequenceEqual(initial.PlayerZones["P1"].MainDeck),"Short look keeps literal available originals.");
            Check(final.Events.All(e=>e.Event.Kind is not("CARDS_REVEALED" or "CARD_DRAWN" or "MAIN_DECK_BURNED_OUT")),"Private LOOK creates no public reveal/draw/burnout event.");
            await File.WriteAllTextAsync(Path.Combine(directory,"all-original-accepted-intents.json"),JsonSerializer.Serialize(originals,json));
            await File.WriteAllTextAsync(Path.Combine(directory,"receipt.json"),JsonSerializer.Serialize(new{status="REAL_SQL_CURRENT_CANDLELIT_PRIVATE_SUBSET_ORDER_SHORT_LOOK_COLD_DEDUP_REPLAY_AND_INDEPENDENT_CAPTURE_REJECTION_PASS",room,branch,commands,events=final.Events.Count,coldRecoveries=cold,directAtomicRejections=rejected,independentStructuralRejections=structural,actualRuleInputs=state.RuleInputs,allOriginalControlledRowsRetained=true,explicitFixtureInitialIsIndependentReplayAuthority=true,originalBlankReplayMetadataNotRewritten=true,normalOpeningFullGameVisibleOrWholePrintingCertification=false},json));
            Console.WriteLine($"C32_SQL_PASS {branch} {commands} commands {final.Events.Count} events {cold} cold {rejected} direct rejects {structural} independent structural rejects");

            async Task Submit(string actor,GameCommand command)
            {
                var intent="c32-"+commands;var raw=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();raw["promptId"]=session.PromptFor(actor).PromptId;raw["snapshotTick"]=state.Tick;var element=JsonSerializer.SerializeToElement(raw,json);
                var accepted=await session.SubmitAsync(actor,intent,command,element,default);Check(accepted.Accepted,accepted.ErrorMessage??"Original command rejected.");state=accepted.State;commands++;originals.Add(new{actor,rawCommand=element});
                var before=await Digest();var frame=await store.LoadAsync(room,default)??throw new Exception("Actual SQL frame required.");Check(frame.IsConsistent&&frame.Commands.Count==commands&&Same(frame.AuthoritativeState,state),"Whole original SQL state and current accepted command count.");
                var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,frame.Commands,state,engine,default,frame.Events);Check(replay.IsMatch,string.Join(';',replay.Errors));
                var restored=MatchSession.Restore(frame,engine,journal,players);
                foreach(var viewer in initial.Seats.Keys)Check(Same(session.SnapshotFor(viewer),restored.SnapshotFor(viewer))&&Same(session.PromptFor(viewer),restored.PromptFor(viewer)),"Cold private snapshot and prompt fields/nulls are exact.");
                session=restored;cold++;Check((await session.SubmitAsync(actor,intent,command,element,default)).Accepted&&await Digest()==before,"Cold duplicate leaves all seven original tables unchanged.");
            }
            async Task Reject(string actor,GameCommand command)
            {
                var before=await Digest();var result=await engine.ResolveAsync(state,new("invalid-c32",actor,command.CmdType),command,default);
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
