using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// Controlled CN OGN·291/298 + SFD·187/221 ordering and JSONB recovery regression; not whole-printing certification.
internal static class CandlelitLegendPendingSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source,string output)
    {
        Check(!Directory.Exists(output),"Never overwrite an original current Candlelit SQL run.");
        Directory.CreateDirectory(output);
        foreach(var branch in new[]{"legend-first","battlefield-first"})await Run(branch);
        async Task Run(string branch)
        {
            var directory=Path.Combine(output,branch);Directory.CreateDirectory(directory);
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
            var room="ordered-legend-pending-"+Guid.NewGuid().ToString("N");var count=3;
            var cards=new Dictionary<string,CardObjectState>();
            foreach(var id in new[]{"DRAW1","DRAW2"})cards[id]=new(id,cardNo:"SFD·125/221",power:4,manaCost:4,tags:[CardObjectTags.UnitCard],ownerId:"P1",controllerId:"P1");
            cards["DRAW3"]=new("DRAW3",cardNo:"OGN·004/298",tags:[CardObjectTags.SpellCard],ownerId:"P1",controllerId:"P1");
            cards["LEGEND"]=new("LEGEND",cardNo:"SFD·187/221",tags:["CARD_TYPE:LEGEND"],ownerId:"P1",controllerId:"P1");
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
            Check(state.TriggerQueue.Count==2&&state.TriggerQueue.Select(t=>t.SourceObjectId).Order().SequenceEqual(new[]{"BF","LEGEND"})&&!state.CardObjects["LEGEND"].IsExhausted,"Both real original conquest captures precede payment or looking.");
            await Save("before-order");
            var ids=state.TriggerQueue.OrderBy(t=>t.SourceObjectId==(branch=="legend-first"?"LEGEND":"BF")?0:1).Select(t=>t.TriggerId).ToArray();
            await Reject("P2",new OrderTriggersCommand(OrderedTriggerIds:ids));
            await Reject("P1",new OrderTriggersCommand(OrderedTriggerIds:[ids[0]]));
            await Reject("P1",new OrderTriggersCommand(OrderedTriggerIds:[ids[0],ids[0]]));
            await Submit("P1",new OrderTriggersCommand(OrderedTriggerIds:ids));
            var cost=state.PendingCardChoice??throw new Exception("Legend trigger exhaust cost must confirm before any look.");
            Check(cost.ChoiceWindow=="TRIGGER_COST_CONFIRMATION"&&cost.PlayerId=="P1"&&cost.LegalObjectIds.SequenceEqual(new[]{"LEGEND"})&&!state.CardObjects["LEGEND"].IsExhausted,"Literal optional source exhaust confirmation.");
            var own=JsonSerializer.Serialize(session.SnapshotFor("P1"),json)+JsonSerializer.Serialize(session.PromptFor("P1"),json);
            Check(!own.Contains("\"DRAW1\"",StringComparison.Ordinal)&&!own.Contains("\"DRAW2\"",StringComparison.Ordinal),"Neither private LOOK nor public REVEAL occurs before the pending cost finalizes.");
            await Save("before-legend-cost");
            await Reject("P2",new ChooseCardsCommand(cost.ChoiceId,cost.ChoiceWindow,["LEGEND"]));
            await Reject("P1",new ChooseCardsCommand("stale-"+cost.ChoiceId,cost.ChoiceWindow,["LEGEND"]));
            await Submit("P1",new ChooseCardsCommand(cost.ChoiceId,cost.ChoiceWindow,["LEGEND"]));
            Check(state.CardObjects["LEGEND"].IsExhausted&&state.StackItems.Count==2&&state.StackItems.Single(i=>i.SourceObjectId=="LEGEND").TriggerCost is {Source.ObjectId:"LEGEND",Source.Generation:0}&&state.PriorityPlayerId=="P1"&&state.PendingCardChoice is null,"Every original pending item confirms before either requested resolution order can look.");
            Check(state.StackItems.Last().SourceObjectId==(branch=="legend-first"?"LEGEND":"BF"),"API selected resolution order and literal stack top agree.");
            await Save("all-pending-confirmed-before-look");
            if(branch=="legend-first")
            {
                await Top();await Legend("DRAW1",["DRAW1","DRAW2"]);
                Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(new[]{"DRAW3","DRAW2"}),"Legend first pays, plays original first unit, then recycles original unplayed second.");
                await Top();await Look(["DRAW3","DRAW2"],["DRAW2","DRAW3"]);
            }
            else
            {
                await Top();await Look(["DRAW1","DRAW2"],["DRAW2","DRAW1"]);
                Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(new[]{"DRAW2","DRAW1","DRAW3"}),"Battlefield first changes only the original retained top order.");
                await Top();await Legend("DRAW2",["DRAW2","DRAW1"]);
            }
            Check(state.PlayerZones["P1"].MainDeck.SequenceEqual(branch=="legend-first"?new[]{"DRAW2","DRAW3"}:new[]{"DRAW3","DRAW1"})&&state.PlayerZones["P1"].Base.Contains(branch=="legend-first"?"DRAW1":"DRAW2"),"Literal final deck and exact played original unit for each independently selected order.");
            Check(state.RunePools["P1"].Mana==16&&state.RunePools["P1"].Power==10&&state.RngCursor==initial.RngCursor&&state.PendingEffectPlay is null,"Grand Fae pays printed four mana and zero power; one recycle never shuffles or leaves a play permission.");
            async Task Top(){Check(state.PriorityPlayerId is not null,"Ordinary response before each original skill resolves.");await Submit(state.PriorityPlayerId!,new PassPriorityCommand());await Submit(state.PriorityPlayerId!,new PassPriorityCommand());}
            async Task Look(string[] expected,string[] ordered)
            {
                var c=state.PendingCardChoice??throw new Exception("Private original conquest LOOK required.");Check(c.ChoiceWindow=="INSIGHT"&&c.PlayerId=="P1"&&c.RequiredCount==0&&c.MaxCount==2&&c.LegalObjectIds.SequenceEqual(expected),"Literal private original top pair for this selected skill order.");
                await Save("private-look");await Submit("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[]));
                c=state.PendingCardChoice!;Check(c.ChoiceWindow=="INSIGHT_ORDER"&&c.LegalObjectIds.SequenceEqual(expected),"Only the same retained original pair can be reordered.");
                await Submit("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,ordered));
            }
            async Task Legend(string selected,string[] expected)
            {
                var c=state.PendingCardChoice??throw new Exception("Public original legend reveal required.");Check(c.PlayerId=="P1"&&c.LegalObjectIds.SequenceEqual(expected),"Literal original legend public top pair after the other skill's chosen effects.");
                var observed=await Save("legend-public-selection");
                var item=state.StackItems.Last();var context=item.DeckPlaySequence!;
                Check(context.Viewed["P1"].Select(card=>card.ObjectId).SequenceEqual(expected),"The actual cold JSONB array preserves the revealed top order independently of object member order.");
                foreach(var changed in new IReadOnlyList<ObjectBinding>[] {
                    context.Viewed["P1"].Reverse().ToArray(),
                    [context.Viewed["P1"][0],context.Viewed["P1"][0]],
                    [context.Viewed["P1"][0] with {Generation=99},context.Viewed["P1"][1]],
                    [new("DRAW3",0),context.Viewed["P1"][1]] })
                {
                    var forged=state with {StackItems=state.StackItems.Take(state.StackItems.Count-1).Append(item with {DeckPlaySequence=context with {
                        Viewed=new Dictionary<string,IReadOnlyList<ObjectBinding>>(context.Viewed){["P1"]=changed}}}).ToArray()};
                    Check(MatchRecoveryValidator.Validate(room,observed.LastEventSequence,observed.Commands,observed.Events,observed.PlayerViews,forged,observed.CurrentTick).Count>0,"Independent ordered observation, duplicate, generation or outside-source forgery rejected.");structural++;
                }
                await Submit("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[selected]));
                var pending=state.PendingEffectPlay??throw new Exception("Original selected card must banish before paid confirmation.");Check(pending.Sources.Keys.SequenceEqual(new[]{selected})&&!pending.IgnoreBaseMana&&state.ObjectLocations[selected].Zone=="STACK"&&state.StackItems.Any(i=>i.SourceObjectId==selected&&!i.SourceConfirmed),"Original selected card has passed through banishment and now awaits paid source confirmation on the stack.");
                var genuine=await Save("legend-banish-before-play");Check(MatchRecoveryValidator.Validate(room,genuine.LastEventSequence,genuine.Commands,genuine.Events,genuine.PlayerViews,state,genuine.CurrentTick).Count==0,"Independent structural validator accepts real SQL source before mutation.");
                foreach(var forged in new[]{pending with {IgnoreBaseMana=true},pending with {Sources=new Dictionary<string,long>{{"DRAW3",state.CardObjects["DRAW3"].ObjectGeneration}}}})
                {Check(MatchRecoveryValidator.Validate(room,genuine.LastEventSequence,genuine.Commands,genuine.Events,genuine.PlayerViews,state with {PendingEffectPlay=forged},genuine.CurrentTick).Count>0,"Independent free-play or outside-original-source forgery rejected.");structural++;}
                await Reject("P2",new PlayCardCommand(selected,"SFD·125/221",[]));
                await Submit("P1",new PlayCardCommand(selected,"SFD·125/221",[]));
            }
            var final=await Save("final");var resultDeck=state.PlayerZones["P1"].MainDeck;
            Check(state.Status==MatchStatuses.InProgress&&state.PlayerScores["P1"]==1&&state.PlayerScores["P2"]==0&&state.PendingCardChoice is null&&state.StackItems.Count==0,"One original conquest; no draw, burnout, extra score or residual window.");
            Check(final.Events.Count(e=>e.Event.Kind=="CARDS_REVEALED")==1&&final.Events.All(e=>e.Event.Kind is not("CARD_DRAWN" or "MAIN_DECK_BURNED_OUT")),"Exactly the legend public reveal; private battlefield LOOK creates no extra reveal, draw or burnout.");
            await File.WriteAllTextAsync(Path.Combine(directory,"all-original-accepted-intents.json"),JsonSerializer.Serialize(originals,json));
            await File.WriteAllTextAsync(Path.Combine(directory,"receipt.json"),JsonSerializer.Serialize(new{status="REAL_SQL_CURRENT_LEGEND_CANDLELIT_BOTH_PENDING_ORDERS_COLD_DEDUP_AND_EXPLICIT_INITIAL_REPLAY",room,branch,commands,events=final.Events.Count,coldRecoveries=cold,directAtomicRejections=rejected,independentStructuralRejections=structural,actualRuleInputs=state.RuleInputs,allOriginalControlledRowsRetained=true,explicitFixtureInitialIsIndependentReplayAuthority=true,originalBlankReplayMetadataNotRewritten=true,normalOpeningFullGameVisibleOrWholePrintingCertification=false},json));
            Console.WriteLine($"LEGEND_PENDING_SQL_PASS {branch} {commands} commands {final.Events.Count} events {cold} cold {rejected} direct rejects {structural} independent structural rejects");

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
