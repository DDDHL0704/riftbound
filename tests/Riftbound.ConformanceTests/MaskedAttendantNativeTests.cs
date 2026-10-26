using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MaskedAttendantNativeTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("standby")]
    [InlineData("replace")]
    [InlineData("decline")]
    public async Task ProductionPlayReplacementAndBothResponseWindowsRestoreAndReplay(string branch)
    {
        var initial = branch == "standby" ? StandbySourceRulesTests.Position(MaskedAttendantTests.Card)
            : branch is "replace" or "decline" ? OfficialTokenReplacementTests.Position(card: MaskedAttendantTests.Card)
            : MaskedAttendantTests.Position();
        initial = initial with { Tick = 0 };
        if (branch=="standby") initial = AttackDamageDistributionTests.Spell(initial,"REPLY","OGN·048/298","P2");
        var source = branch == "standby" ? "SHADOW" : branch is "replace" or "decline" ? "C" : "MASK";
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MASKED_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));
        }
        async Task<ResolutionResult> Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name+"-prompt.json",prompt);
            Export(name+"-snapshot.json",ResolutionResult.BuildSnapshots(current)[actor]); Export(name+"-selection.json",fallback);
            if (fallback is PlayCardCommand play) Export(name+"-quote.json",engine.PreviewPlayCard(current,actor,new("quote",prompt.PromptId!,current.Tick,play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(fallback,fallback.GetType(),json))!;
            if (fallback is PlayCardCommand p) fields["optionalCosts"] = JsonSerializer.SerializeToElement(p.OptionalCosts ?? []);
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir,name+"-command.json"))) {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+"-command.json")));
                raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString()); Assert.Equal(current.Tick,raw.GetProperty("snapshotTick").GetInt64());
            var result = await session.SubmitAsync(actor,name,command,raw,default); Assert.True(result.Accepted,result.ErrorMessage);
            current = result.State; OfficialGraveyardRecastTests.Restore(current); ArmamentMightTests.PublicRecovery(current); steps.Add(name);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted); Assert.Equal(count,journal.Entries.Count);
            var seq = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(current); var prompts = ResolutionResult.BuildPrompts(current);
            var views = current.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,current.Tick,seq,snapshots[id],current.Tick,seq,prompts[id]));
            var errors = MatchRecoveryValidator.Validate(current.RoomId,seq,Commands(),Events(),views,current,current.Tick);
            Assert.True(errors.Count==0,string.Join("; ",errors));
            session = MatchSession.Restore(new(current.RoomId,current.Tick,seq,Commands(),Events(),views,errors,current,initial),engine,journal);
            Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted); Assert.Equal(count,journal.Entries.Count);
            var stale = await session.SubmitAsync(actor,"stale-"+name,command,raw,default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired,stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(current),MatchStateHasher.Hash(stale.State));
            return result;
        }
        if (branch=="standby") {
            await Submit("setup-hide","P1",new HideCardCommand("SHADOW",MaskedAttendantTests.Card,"BATTLEFIELD:F",["STANDBY_A"]));
            await Submit("setup-end","P1",new EndTurnCommand());
            foreach(var i in new[]{1,2,3}) await Submit("setup-tap-"+i,"P2",new TapRuneCommand("SHADOW-P2-RUNE-"+i));
            foreach(var i in new[]{4,5}) await Submit("setup-recycle-"+i,"P2",new RecycleRuneCommand("SHADOW-P2-RUNE-"+i));
            await Submit("setup-reply","P2",new PlayCardCommand("REPLY","OGN·048/298",[],OptionalCosts:[]));
            await Submit("setup-offer","P2",new PassPriorityCommand());
        }
        await Submit("play","P1",new PlayCardCommand(source,MaskedAttendantTests.Card,[],OptionalCosts:[]));
        Assert.Empty(MaskedAttendantTests.Images(current));
        for (var i=0;i<2;i++) await Submit("entry-pass-"+i,current.PriorityPlayerId!,new PassPriorityCommand());
        var choiceIndex = 0;
        while (current.PendingCardChoice is { } choice) {
            Assert.Equal("TOKEN_ENTRY_REPLACEMENT",choice.ChoiceWindow);
            await Submit("replacement-"+choiceIndex++,choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,branch=="replace"?["Z1"]:[]));
        }
        var expected = branch=="replace"?3:2;
        Assert.Equal(expected,MaskedAttendantTests.Images(current).Length);
        Assert.All(MaskedAttendantTests.Images(current),image=>{Assert.Equal(0,image.Power);Assert.DoesNotContain(CardObjectTags.Ephemeral,image.Tags);});
        for (var i=0;i<2;i++) await Submit("copy-pass-"+i,current.PriorityPlayerId!,new PassPriorityCommand());
        Assert.All(MaskedAttendantTests.Images(current),image=>{
            Assert.Equal(MaskedAttendantTests.Card,image.CardNo);Assert.Equal(1,image.Power);Assert.Equal(2,image.ManaCost);Assert.True(image.IsExhausted);
            Assert.Equal(branch=="standby"?"F":null,current.ObjectLocations[image.ObjectId].BattlefieldObjectId);
        });
        Assert.DoesNotContain(current.StackItems,item=>item.ReflexiveCopy is not null || item.SourceObjectId==source);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,Commands(),current,engine,default,Events());
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("steps.json",steps);
        Export("receipt.json",new { branch,native,commands=journal.Entries.Count,restores=steps.Count,events=Events().Length,replay });
        if (native>0) Assert.Equal(steps.Count(step=>!step.StartsWith("setup-")),native);
        RecoveredCommand[] Commands()=>journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,
            e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events()=>journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken cancellationToken) {Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
