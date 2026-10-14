using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class MaskedAttendantSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var room="postgres-masked-"+Guid.NewGuid().ToString("N");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var initial=new MatchState(room,tick:0,turnNumber:1,activePlayerId:"P1",seats:new Dictionary<string,string>{{"P1","P1"},{"P2","P2"}},seed:424242,
            status:MatchStatuses.InProgress,phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,
            runePools:new Dictionary<string,RunePool>{{"P1",new(10,10)},{"P2",new(10,10)}},
            playerZones:new Dictionary<string,PlayerZones>{{"P1",PlayerZones.Empty with {Hand=["MASK"]}},{"P2",PlayerZones.Empty}},
            cardObjects:new Dictionary<string,CardObjectState>{{"MASK",new("MASK",cardNo:"UNL-081/219",power:1,manaCost:2,
                ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])}},
            objectLocations:new Dictionary<string,ObjectLocationState>{{"MASK",new("P1","HAND")}});
        var engine=new CoreRuleEngine();var journal=new PostgresMatchJournal(source);var players=new PostgresMatchPlayerStore(source);
        var store=new PostgresMatchRecoveryStore(source);IMatchSession session=new MatchSession(initial,engine,journal,players);
        var current=initial;var checks=new List<object>();var submitted=0;long sequence=0;
        foreach(var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room,actor,actor,ReconnectTokenHasher.Hash("audit-"+actor),default);
        await File.WriteAllTextAsync(Path.Combine(output,"explicit-fixture-initial.json"),JsonSerializer.Serialize(initial,json));
        try {
            await Submit("P1",new PlayCardCommand("MASK","UNL-081/219",[],OptionalCosts:[]));
            Check(current.StackItems.Single().SelfCopySource==new ObjectBinding("MASK",1),"Durable play must bind the entered incarnation.");
            for(var i=0;i<2;i++)await Submit(current.PriorityPlayerId!,new PassPriorityCommand());
            var images=current.CardObjects.Values.Where(c=>c.TokenFactoryCardNo==P6TokenFactoryCatalog.ImageTokenCardNo).ToArray();
            Check(images.Length==2 && images.All(c=>c.Power==0 && c.CardNo==P6TokenFactoryCatalog.ImageTokenCardNo),"SQL copy window must retain two raw Images.");
            Check(current.StackItems.Single().ReflexiveCopy?.CopySourceIsSelf==true,"Durable copy context lost its untargeted self reference.");
            for(var i=0;i<2;i++)await Submit(current.PriorityPlayerId!,new PassPriorityCommand());
            images=current.CardObjects.Values.Where(c=>c.TokenFactoryCardNo==P6TokenFactoryCatalog.ImageTokenCardNo).ToArray();
            Check(images.Length==2 && images.All(c=>c.CardNo=="UNL-081/219" && c.Power==1 && c.ManaCost==2 && c.IsExhausted
                && c.Tags.Contains(CardObjectTags.Ephemeral)),"SQL restored copies do not match independent printed/readiness expectations.");
            var frame=(await store.LoadAsync(room,default))!;
            await File.WriteAllTextAsync(Path.Combine(output,"receipt.json"),JsonSerializer.Serialize(new {
                status="REAL_POSTGRES_MASKED_SELF_COPY_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS",room,submitted,events=sequence,coldRecoveries=checks.Count,
                checks,finalHash=MatchStateHasher.Hash(current),ruleInputs=current.RuleInputs,
                scope="New isolated explicit rules fixture; SQL commands/events/states/views are real. Replay uses the retained explicit fixture initial state, not a fabricated normal opening. The previous paired normal-score history is unchanged."},json));
            Console.WriteLine($"POSTGRES_MASKED_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection=await source.OpenConnectionAsync();
            await using var cleanup=new NpgsqlCommand("delete from matches where match_id=@room",connection);cleanup.Parameters.AddWithValue("room",room);await cleanup.ExecuteNonQueryAsync();
        }
        async Task Submit(string actor,GameCommand command) {
            var prompt=session.PromptFor(actor);var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(command,command.GetType(),json))!;
            fields["promptId"]=JsonSerializer.SerializeToElement(prompt.PromptId);fields["snapshotTick"]=JsonSerializer.SerializeToElement(current.Tick);
            var result=await session.SubmitAsync(actor,"masked-sql-"+submitted,command,JsonSerializer.SerializeToElement(fields),default);
            Check(result.Accepted,result.ErrorMessage);current=result.State;submitted++;sequence+=result.Events.Count;
            var frame=await store.LoadAsync(room,default)??throw new InvalidOperationException("Missing actual SQL frame.");
            Check(frame.IsConsistent,string.Join("; ",frame.ValidationErrors));
            Check(frame.Commands.Count==submitted && frame.Events.Count==sequence && MatchStateHasher.Hash(frame.AuthoritativeState!)==MatchStateHasher.Hash(current),"SQL history/state mismatch.");
            await File.WriteAllTextAsync(Path.Combine(output,"checkpoint-"+submitted+"-events.json"),JsonSerializer.Serialize(new {
                live=result.Events.Select(ev=>new {ev.Kind,payload=JsonSerializer.SerializeToElement(ev.Payload),hash=MatchStateHasher.HashValue(ev.Payload)}),
                stored=frame.Events.Where(ev=>ev.Tick==current.Tick).Select(ev=>new {ev.Event.Kind,payload=JsonSerializer.SerializeToElement(ev.Event.Payload),hash=MatchStateHasher.HashValue(ev.Event.Payload)})},json));
            var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,frame.Commands,frame.AuthoritativeState!,engine,default,frame.Events);
            Check(replay.IsMatch,string.Join("; ",replay.Errors));
            session=MatchSession.Restore(frame,engine,journal,players);
            checks.Add(new {current.Tick,sequence,stack=current.StackItems.Count,replay.IsMatch});
        }
    }
    private static void Check(bool condition,string? message) {if(!condition)throw new InvalidOperationException(message);}
}
