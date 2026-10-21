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
    public static Task RunAsync(NpgsqlDataSource source,string output)
        => RunBranchesAsync(source,output,["keep-reverse","recycle-one","recycle-two","short-empty","short-one","nocturne-zero","nocturne-one","nocturne-two","optional-decline","unaffordable-decline","decline-first-play-second"]);

    public static Task RunObservedAsync(NpgsqlDataSource source,string output)
        => RunBranchesAsync(source,output,["nocturne-zero","nocturne-one","nocturne-two"]);

    public static Task RunOptionalObservedAsync(NpgsqlDataSource source,string output)
        => RunBranchesAsync(source,output,["optional-decline","unaffordable-decline","decline-first-play-second"]);

    private static async Task RunBranchesAsync(NpgsqlDataSource source,string output,IReadOnlyList<string> branches)
    {
        Check(!Directory.Exists(output),"Never overwrite an original current Candlelit SQL run.");
        Directory.CreateDirectory(output);
        foreach(var branch in branches)await Run(branch);
        async Task Run(string branch)
        {
            var directory=Path.Combine(output,branch);Directory.CreateDirectory(directory);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
            var room="candlelit-private-look-"+Guid.NewGuid().ToString("N");var count=branch=="short-empty"?0:branch=="short-one"?1:3;
            var cards=new Dictionary<string,CardObjectState>();
            foreach(var id in new[]{"DRAW1","DRAW2","DRAW3"})cards[id]=new(id,cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1");
            var optionalObserved=branch is "optional-decline" or "unaffordable-decline" or "decline-first-play-second";var unaffordable=branch=="unaffordable-decline";
            var banish=branch switch {"nocturne-zero"=>0,"nocturne-one" or "optional-decline" or "unaffordable-decline"=>1,"nocturne-two" or "decline-first-play-second"=>2,_=>-1};
            if(banish>=0)foreach(var id in new[]{"DRAW1","DRAW2"})cards[id]=new(id,cardNo:"OGN·194/298",power:4,manaCost:4,tags:[CardObjectTags.UnitCard,"游走"],ownerId:"P1",controllerId:"P1");
            cards["BF"]=new("BF",cardNo:"OGN·291/298",tags:[P6TokenFactoryCatalog.BattlefieldCardTag],ownerId:"P2");
            cards["UNIT"]=new("UNIT",cardNo:"OGN·096/298",power:1,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1");
            cards["RUNE"]=new("RUNE",cardNo:"OGN·007/298",tags:[CardObjectTags.RuneCard],ownerId:"P1",controllerId:"P1",isExhausted:true);cards["RUNE2"]=cards["RUNE"] with {ObjectId="RUNE2"};
            if(unaffordable){cards.Remove("RUNE");cards.Remove("RUNE2");}
            var deck=new[]{"DRAW1","DRAW2","DRAW3"};
            var initial=new MatchState(room,0,5,"P1",new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},status:MatchStatuses.InProgress,readyPlayerIds:["P1","P2"],turnPlayerId:"P1",phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
                playerZones:new Dictionary<string,PlayerZones>{["P1"]=PlayerZones.Empty with {Base=unaffordable?["UNIT"]:["UNIT","RUNE","RUNE2"],Hand=deck.Skip(count).ToArray(),MainDeck=deck.Take(count).ToArray()},["P2"]=PlayerZones.Empty with {Battlefields=["BF"]}},
                cardObjects:cards,playerScores:new Dictionary<string,int>{{"P1",0},{"P2",0}},runePools:new Dictionary<string,RunePool>{{"P1",new(10,unaffordable?0:10)},{"P2",RunePool.Empty}});
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
            if(banish>=0)
            {
                for(var i=0;i<2;i++)
                {
                    var request=state.PendingRuleChoice?.Request??throw new Exception("Original private observation replacement required.");
                    Check(request.PlayerId=="P1"&&request.Kind=="DECK_OBSERVATION_BANISH"&&state.PendingCardChoice is null&&state.PendingEffectPlay is null,"Both original observation decisions precede parent subset/order and pending play.");
                    var other=JsonSerializer.Serialize(session.SnapshotFor("P2"),json)+JsonSerializer.Serialize(session.PromptFor("P2"),json);
                    foreach(var id in state.PlayerZones["P1"].MainDeck.Except(deck.Take(Math.Min(i,banish))))Check(!other.Contains("\""+id+"\"",StringComparison.Ordinal),"Original cards not already publicly banished stay private during the observation decision.");
                    await Save("private-observation-"+i);
                    await Reject("P2",new PayCostCommand(request.Id,"RULE_REPLACEMENT",["KEEP"]));
                    await Reject("P1",new PayCostCommand("stale-"+request.Id,"RULE_REPLACEMENT",["KEEP"]));
                    await Reject("P1",new PayCostCommand(request.Id,"RULE_REPLACEMENT",["DRAW3"]));
                    await Submit("P1",new PayCostCommand(request.Id,"RULE_REPLACEMENT",[i<banish?"BANISH":"KEEP"]));
                }
                Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(deck.Skip(banish)),"Only the original top pair is observed; a removed card never refills from DRAW3.");
                Check((state.ObservedDeckPlays??[]).Select(p=>p.Card.ObjectId).SequenceEqual(deck.Take(banish)),"Existing pending insertion history retains precisely the original accepted captures.");
            }
            if(count==0)Check(state.PendingCardChoice is null&&state.StackItems.Count==0,"Empty private look finishes without impossible choice.");
            else if(state.PendingCardChoice is { } choice)
            {
                var viewed=banish>=0?deck.Take(2).Skip(banish).ToArray():deck.Take(Math.Min(2,count)).ToArray();
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
            else Check(banish==2,"Only replacing both original observations can omit the nonempty parent subset.");
            if(banish>=0)
            {
                foreach(var id in deck.Take(banish))
                {
                    var pending=state.PendingEffectPlay??throw new Exception("Earliest captured observation must await confirmation.");
                    Check(state.PendingCardChoice is null&&pending.PlayerId=="P1"&&pending.Sources.Keys.SequenceEqual(new[]{id}),"Parent subset/order finishes before the earliest already-captured pending item can confirm.");
                    await Save("before-original-pending-"+id);
                    await Reject("P2",new PlayCardCommand(id,"OGN·194/298",[]));
                    if(id=="DRAW1"&&banish==2)await Reject("P1",new PlayCardCommand("DRAW2","OGN·194/298",[]));
                    if(optionalObserved&&id=="DRAW1")
                    {
                        var play=new PlayCardCommand(id,"OGN·194/298",[]);
                        var quote=engine.PreviewPlayCard(state,"P1",new("optional-quote-"+state.Tick,session.PromptFor("P1").PromptId!,state.Tick,play));
                        Check(pending.Optional&&quote.CanPay==!unaffordable,"Exact optional permission; power exists or no pool power/base rune can pay.");
                        var decline=new ChooseCardsCommand(pending.ChoiceId,"EFFECT_PLAY",[]);
                        await Reject("P2",decline);await Reject("P1",new ChooseCardsCommand("stale-"+pending.ChoiceId,"EFFECT_PLAY",[]));
                        if(unaffordable)await Reject("P1",play);
                        await Submit("P1",decline);
                        Check(state.PlayerZones["P1"].Banished.Contains(id)&&!state.PlayerZones["P1"].Base.Contains(id),"Declined original source stays banished with no automatic payment or play.");
                    }
                    else
                    {
                        await Submit("P1",new PlayCardCommand(id,"OGN·194/298",[]));
                        Check(state.PlayerZones["P1"].Base.Contains(id)&&state.CardObjects[id].CardNo=="OGN·194/298","Original confirmed unit enters its owner's base.");
                    }
                }
                var expected=banish==0?new[]{"DRAW2","DRAW1","DRAW3"}:banish==1?new[]{"DRAW2","DRAW3"}:new[]{"DRAW3"};
                Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(expected)&&state.PendingEffectPlay is null&&state.PendingRuleChoice is null&&state.ObservedDeckPlays is null,"Exact retained original deck and no residual observation or pending permission.");
                Check(state.RunePools["P1"].Mana==initial.RunePools["P1"].Mana&&state.RunePools["P1"].Power==initial.RunePools["P1"].Power-(optionalObserved?(banish==2?1:0):banish)&&state.RngCursor==initial.RngCursor,"Each accepted original Nocturne pays exactly one generic power, without base mana or recycling RNG.");
            }
            var final=await Save("final");var resultDeck=state.PlayerZones["P1"].MainDeck;
            if(optionalObserved)Check(state.PlayerZones["P1"].Banished.SequenceEqual(new[]{"DRAW1"})&&final.Events.Count(e=>e.Event.Kind=="EFFECT_PLAY_DECLINED")==1&&final.Events.Count(e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE")== (banish==2?1:0),"Exactly one explicit original decline and only the optional second actual play.");
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
