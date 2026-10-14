using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuntimeRuleInputIdentityTests
{
    [Fact]
    public async Task NormalJournalCarriesTheSameActualRuleCatalogAndImplementationIdentityAsItsState()
    {
        var initial=MatchReplayInitialStateBuilder.FromSeats("runtime-input-identity",new Dictionary<string,string>{["P1"]="P1",["P2"]="P2"});
        var journal=new RecordingJournal();var session=new MatchSession(initial,new CoreRuleEngine(),journal);
        await session.ReadyAsync("P1","ready-identity",JsonSerializer.SerializeToElement(new {cmdType="READY"}),default);
        var entry=Assert.Single(journal.Entries);
        var recorded=JsonSerializer.SerializeToElement(entry);
        Assert.True(recorded.TryGetProperty("RuleInputs",out var inputs),"Normal journal has no actual rule/catalog/implementation input identity.");
        var state=JsonSerializer.SerializeToElement(entry.AuthoritativeState);
        Assert.True(state.TryGetProperty("RuleInputs",out var stateInputs),"Authoritative state has no pinned rule input identity.");
        Assert.Equal(inputs.GetRawText(),stateInputs.GetRawText());
        Assert.Equal("CN",inputs.GetProperty("Region").GetString());
        Assert.Equal(64,inputs.GetProperty("CatalogSha256").GetString()!.Length);
        Assert.Equal(64,inputs.GetProperty("ImplementationSha256").GetString()!.Length);
        Assert.Equal(RuleInputIdentity.Current.IdentitySha256,entry.RulesetVersion);
        Assert.Equal(RuleInputIdentity.Current.AuthoritySha256,entry.FaqVersion);
        entry.ValidateRuleInputs();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("schema")]
    [InlineData("region")]
    [InlineData("language")]
    [InlineData("authority")]
    [InlineData("catalog")]
    [InlineData("implementation")]
    [InlineData("fingerprint")]
    public async Task MismatchedInputsRejectCoreSessionRecoveryAndEvenSelfConsistentReplay(string branch)
    {
        var state=Initial() with {RuleInputs=Wrong(branch)};
        var result=await new CoreRuleEngine().ResolveAsync(state,new("wrong-inputs","P1","PASS"),new PassCommand(),default);
        Assert.False(result.Accepted);Assert.Equal(ErrorCodes.RecoveryInconsistent,result.ErrorCode);
        Assert.Same(state,result.State);Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(()=>new MatchSession(state,new CoreRuleEngine(),NoopMatchJournal.Instance));
        var errors=MatchRecoveryValidator.Validate(state.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),state,0);
        Assert.Contains(errors,e=>e.Contains("rule input identity",StringComparison.Ordinal));
        var frame=new MatchRecoveryFrame(state.RoomId,0,0,[],[],new Dictionary<string,RecoveredPlayerView>(),[],state,state);
        Assert.Throws<MatchSessionException>(()=>MatchSession.Restore(frame,new CoreRuleEngine(),NoopMatchJournal.Instance));
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame,new CoreRuleEngine(),default));
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(state,[],state,new CoreRuleEngine(),default);
        Assert.Equal(replay.ReplayedStateHash,replay.ExpectedStateHash);
        Assert.False(replay.IsMatch);Assert.NotEmpty(replay.Errors);
    }

    [Fact]
    public void SerializedStateMustContainExplicitIdentityAndCannotFillMissingDevelopmentHistory()
    {
        var json=JsonSerializer.SerializeToNode(Initial())!.AsObject();
        json.Remove("RuleInputs");
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<MatchState>(json.ToJsonString()));
        json["RuleInputs"]=null;
        var unbound=JsonSerializer.Deserialize<MatchState>(json.ToJsonString())!;
        Assert.Throws<MatchSessionException>(()=>new MatchSession(unbound,new CoreRuleEngine(),NoopMatchJournal.Instance));
        var frame=new MatchRecoveryFrame("unbound",0,0,[],[],new Dictionary<string,RecoveredPlayerView>(),[]);
        Assert.Throws<MatchSessionException>(()=>MatchSession.Restore(frame,new CoreRuleEngine(),NoopMatchJournal.Instance));
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("preview")]
    public async Task PendingChoiceCannotReintroduceDifferentRuleInputs(string branch)
    {
        var state=Initial();var wrong=state with {RuleInputs=Wrong("catalog")};
        var pending=new RuleChoiceContinuation(branch=="origin"?wrong:state,new("pending","P1","PASS"),
            JsonSerializer.SerializeToElement(new {cmdType="PASS"}),[],
            new("choice","P1","test",[new("yes","Yes")],Preview:branch=="preview"?wrong:state));
        state=state with {PendingRuleChoice=pending};
        var result=await new CoreRuleEngine().ResolveAsync(state,new("choose","P1","SURRENDER"),new SurrenderCommand(),default);
        Assert.False(result.Accepted);Assert.Same(state,result.State);Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(()=>new MatchSession(state,new CoreRuleEngine(),NoopMatchJournal.Instance));
    }

    [Fact]
    public async Task RecoveredCommandIdentityCannotBeSubstitutedEvenWhenStatesMatch()
    {
        var state=Initial();var raw=JsonSerializer.SerializeToElement(new {cmdType="READY"});
        var command=new RecoveredCommand("P1","ready","READY",raw,0,1,0,1,true,null) {RuleInputs=Wrong("implementation")};
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(state,[command],state,new CoreRuleEngine(),default);
        Assert.False(replay.IsMatch);Assert.Contains(replay.Errors,e=>e.Contains("rule input identity",StringComparison.Ordinal));
        var frame=new MatchRecoveryFrame(state.RoomId,0,0,[command],[],new Dictionary<string,RecoveredPlayerView>(),[],state,state);
        Assert.Throws<MatchSessionException>(()=>MatchSession.Restore(frame,new CoreRuleEngine(),NoopMatchJournal.Instance));
    }

    [Theory]
    [InlineData("journal")]
    [InlineData("ruleset")]
    [InlineData("faq")]
    public async Task JournalRejectsIdentityOrVersionLabelsWhichDifferFromRecordedState(string branch)
    {
        var journal=new RecordingJournal();var session=new MatchSession(Initial(),new CoreRuleEngine(),journal);
        await session.ReadyAsync("P1","ready",JsonSerializer.SerializeToElement(new {cmdType="READY"}),default);
        var entry=Assert.Single(journal.Entries);
        var wrong=branch switch {
            "journal"=>entry with {RuleInputs=Wrong("authority")},
            "ruleset"=>entry with {RulesetVersion="rules-260330"},
            _=>entry with {FaqVersion="official-pdf-faq-set-2026-04-28"}};
        Assert.Throws<MatchSessionException>(wrong.ValidateRuleInputs);
    }

    private static MatchState Initial()=>MatchReplayInitialStateBuilder.FromSeats("runtime-input-identity",
        new Dictionary<string,string>{["P1"]="P1",["P2"]="P2"});

    [Fact]
    public async Task DefaultCatalogConsumersShareAnImmutableActualSnapshot()
    {
        var first=await OfficialCardCatalog.LoadDefaultAsync();var second=await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Same(first,second);
        var cards=Assert.IsAssignableFrom<IList<OfficialCard>>(first.Cards);
        Assert.Throws<NotSupportedException>(()=>cards[0]=cards[0] with {CardEffect="different effect"});
        var colors=Assert.IsAssignableFrom<IList<string>>(cards[0].CardColorList);
        Assert.Throws<NotSupportedException>(()=>colors.Add("different color"));
    }

    private static RuleInputIdentity? Wrong(string branch)
    {
        var current=RuleInputIdentity.Current;
        return branch switch {
            "missing"=>null,
            "schema"=>current with {SchemaVersion=2},
            "region"=>current with {Region="GLOBAL"},
            "language"=>current with {Language="en-US"},
            "authority"=>current with {AuthoritySha256=new('a',64)},
            "catalog"=>current with {CatalogSha256=new('b',64)},
            "fingerprint"=>current with {IdentitySha256=new('d',64)},
            _=>current with {ImplementationSha256=new('c',64)}};
    }

    [Theory]
    [InlineData("IdentitySha256")]
    [InlineData("CatalogSha256")]
    [InlineData("AuthoritySha256")]
    [InlineData("ImplementationSha256")]
    public void SerializedIdentityRequiresEveryRecordedInputAndRejectsForgedFingerprint(string field)
    {
        var json=JsonSerializer.SerializeToNode(RuleInputIdentity.Current)!.AsObject();json.Remove(field);
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<RuleInputIdentity>(json.ToJsonString()));
        json=JsonSerializer.SerializeToNode(RuleInputIdentity.Current)!.AsObject();json[field]=new string('e',64);
        var wrong=JsonSerializer.Deserialize<RuleInputIdentity>(json.ToJsonString());
        Assert.False(RuleInputIdentity.IsCurrent(wrong));
    }

    private sealed class RecordingJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token)
        {
            Entries.Add(entry);return ValueTask.CompletedTask;
        }
    }
}
