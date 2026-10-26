using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
namespace Riftbound.ConformanceTests;

// Hostile copies of an actual accepted fixture, never changed accepted history.
// Coherent attacks also retain explicit original-birth action/event replay.
public sealed class BattleLimitedRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<Task<Fixture>> Actual = new(Build);
    private const string GrantPrefix = "BATTLE_LIMITED_GRANT:";
    private const string RolePrefix = "BATTLE_ROLE_FIRST:";
    private const string ScopePrefix = "BATTLE_LIMITED_SCOPE:";
    private sealed record Fixture(MatchState Initial, MatchState State, RecoveredCommand[] Commands,
        RecoveredEvent[] Events, MatchState Checkpoint, RecoveredCommand[] CheckpointCommands,
        RecoveredEvent[] CheckpointEvents);

    [Theory]
    [InlineData("unknown-grant-field", false)]
    [InlineData("unknown-context-field", false)]
    [InlineData("missing-required-role-condition", false)]
    [InlineData("duplicate-grant-key", false)]
    [InlineData("wrong-definition", false)]
    [InlineData("wrong-controller", false)]
    [InlineData("wrong-target-generation", false)]
    [InlineData("wrong-source-generation", false)]
    [InlineData("wrong-copy-origin-generation", false)]
    [InlineData("wrong-snapshot", false)]
    [InlineData("wrong-power-duration", false)]
    [InlineData("first-condition-false", false)]
    [InlineData("duplicate-role-identity", false)]
    [InlineData("duplicate-battle-scope", false)]
    [InlineData("unpaired-power-ledger", false)]
    [InlineData("coherent-unearned-stack", true)]
    [InlineData("coherent-earlier-birth", true)]
    public async Task RejectMalformedOrCoherentlyReboundBattleEffects(string forgery, bool replay)
    {
        var f = await Actual.Value;
        var originalHash = MatchStateHasher.Hash(f.State);
        Assert.Empty(Validate(f.State, f.Commands, f.Events));
        var node = JsonSerializer.SerializeToNode(f.State, Json)!.AsObject();
        switch (forgery)
        {
            case "unknown-grant-field": Grant(node, g => g["unknown"] = 1); break;
            case "unknown-context-field": Grant(node, g => g["context"]!["unknown"] = 1); break;
            case "missing-required-role-condition": Role(node, r => r.Remove("oneOnOne")); break;
            case "duplicate-grant-key":
                var list = Effects(node);
                var duplicateIndex = Enumerable.Range(0, list.Count).First(index => list[index]!.GetValue<string>().StartsWith(GrantPrefix));
                list[duplicateIndex] = GrantPrefix + "{\"targetGeneration\":1," + list[duplicateIndex]!.GetValue<string>()[(GrantPrefix.Length + 1)..];
                break;
            case "wrong-definition": Grant(node, g => g["context"]!["cardNo"] = "OGN·210/298"); break;
            case "wrong-controller": Grant(node, g => g["controllerId"] = "P1"); break;
            case "wrong-target-generation": Grant(node, g => g["targetGeneration"] = g["targetGeneration"]!.GetValue<long>() + 1); break;
            case "wrong-source-generation": Grant(node, g => g["sourceGeneration"] = g["sourceGeneration"]!.GetValue<long>() + 1); break;
            case "wrong-copy-origin-generation": Grant(node, g => g["context"]!["origin"]!["unit"]!["generation"] = 99, true); break;
            case "wrong-snapshot": Grant(node, g => g["snapshotMight"] = 99); break;
            case "wrong-power-duration": Powers(node)[0]!["duration"] = "UNTIL_END_OF_TURN"; break;
            case "first-condition-false": Role(node, r => r["oneOnOne"] = false); break;
            case "duplicate-role-identity":
                var roles = node["untilEndOfTurnEffects"]!.AsArray();
                var text = roles.Select(x => x!.GetValue<string>()).First(x => x.StartsWith(RolePrefix));
                var role = JsonNode.Parse(text[RolePrefix.Length..])!.AsObject();
                role["acquiredAtTick"] = role["acquiredAtTick"]!.GetValue<long>() + 1;
                roles.Add(RolePrefix + role.ToJsonString(Json)); break;
            case "duplicate-battle-scope":
                var scopes = node["untilEndOfTurnEffects"]!.AsArray();
                var scopeText = scopes.Select(x => x!.GetValue<string>()).First(x => x.StartsWith(ScopePrefix));
                var scope = JsonNode.Parse(scopeText[ScopePrefix.Length..])!.AsObject();
                scope["roleEventOrder"] = scope["roleEventOrder"]!.GetValue<int>() + 1;
                scopes.Add(ScopePrefix + scope.ToJsonString(Json)); break;
            case "unpaired-power-ledger":
                var effects = Effects(node);
                effects.RemoveAt(Enumerable.Range(0, effects.Count).First(i => effects[i]!.GetValue<string>().StartsWith(GrantPrefix))); break;
            case "coherent-unearned-stack": Grant(node, g => { g["stackItemId"] = "unearned-stack"; RebindPower(node, g); }); break;
            case "coherent-earlier-birth": EarlierBirth(node); break;
            default: throw new InvalidOperationException(forgery);
        }
        var state = node.Deserialize<MatchState>(Json)!;
        // Coherent public rows are attack inputs; original accepted events are
        // the independent witness, never a regenerated expected event stream.
        Assert.NotEmpty(Validate(state, f.Commands, f.Events));
        if (replay)
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(f.Initial, f.Commands,
                state, new CoreRuleEngine(), default, f.Events);
            Assert.False(result.IsMatch);
        }
        Assert.Equal(originalHash, MatchStateHasher.Hash(f.State));
    }

    [Fact]
    public async Task RejectDuplicateRoleEventCoordinatesWithoutThrowing()
    {
        var f = await Actual.Value;
        var role = f.Events.First(e => e.Event.Kind == "BATTLE_ROLES_ESTABLISHED");
        var donor = Array.FindIndex(f.Events, e => e.Tick == role.Tick && e.Order != role.Order && e.Sequence < role.Sequence);
        Assert.True(donor >= 0);
        var forged = f.Events.ToArray();
        forged[donor] = forged[donor] with { Order = role.Order, Event = role.Event };
        Assert.NotEmpty(Validate(f.State, f.Commands, forged));
        Assert.NotEqual(role.Order, f.Events[donor].Order);
    }

    [Fact]
    public async Task UntargetedSelfSkillCannotAcquireATarget()
    {
        var f = await Actual.Value; var item = f.Checkpoint.StackItems[0];
        var targeted = item with { TargetObjectIds = ["HOST"], TargetGenerations = new Dictionary<string,long> {
            ["HOST"] = f.Checkpoint.CardObjects["HOST"].ObjectGeneration } };
        var state = f.Checkpoint with { StackItems = f.Checkpoint.StackItems.Select(i => i.StackItemId == item.StackItemId ? targeted : i).ToArray() };
        Assert.NotEmpty(Validate(state, f.CheckpointCommands, f.CheckpointEvents));
        Assert.Empty(item.TargetObjectIds); Assert.Null(item.TargetGenerations);
    }

    [Fact]
    public async Task ComponentDiagnosticsCannotDowngradeNormalRecoveryWhenHistoryIsErased()
    {
        var f = await Actual.Value;
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(f.State));
        var errors = MatchRecoveryValidator.Validate(f.State.RoomId, 0, [], [],
            new Dictionary<string,RecoveredPlayerView>(), f.State, f.State.Tick);
        Assert.Contains(errors, e => e.Contains("battle scope has no matching accepted initial role event"));
    }

    private static IReadOnlyList<string> Validate(MatchState state, RecoveredCommand[] commands, RecoveredEvent[] events)
    {
        var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            snapshots[p], state.Tick, sequence, prompts[p]));
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }

    private static JsonArray Effects(JsonObject state) => state["cardObjects"]!["ATTACKER"]!["untilEndOfTurnEffects"]!.AsArray();
    private static JsonArray Powers(JsonObject state) => state["cardObjects"]!["ATTACKER"]!["untilEndOfTurnPowerModifiers"]!.AsArray();
    private static void Grant(JsonObject state, Action<JsonObject> change, bool copied = false)
    {
        var list = Effects(state);
        for (var i = 0; i < list.Count; i++)
        {
            var text = list[i]!.GetValue<string>(); if (!text.StartsWith(GrantPrefix)) continue;
            var grant = JsonNode.Parse(text[GrantPrefix.Length..])!.AsObject();
            if (copied && grant["context"]!["origin"] is null) continue;
            change(grant); list[i] = GrantPrefix + grant.ToJsonString(Json); return;
        }
        throw new InvalidOperationException("Actual fixture has no captured grant.");
    }
    private static void Role(JsonObject state, Action<JsonObject> change)
    {
        var list = state["untilEndOfTurnEffects"]!.AsArray();
        for (var i = 0; i < list.Count; i++)
        {
            var text = list[i]!.GetValue<string>(); if (!text.StartsWith(RolePrefix)) continue;
            var role = JsonNode.Parse(text[RolePrefix.Length..])!.AsObject();
            if (role["sourceObjectId"]!.GetValue<string>() != "ATTACKER") continue;
            change(role); list[i] = RolePrefix + role.ToJsonString(Json); return;
        }
        throw new InvalidOperationException("Actual fixture has no source role.");
    }
    private static void RebindPower(JsonObject state, JsonObject grant)
    {
        var power = Powers(state).Single(p => p!["effectId"]!.GetValue<string>() == grant["powerEffectId"]!.GetValue<string>())!.AsObject();
        var identity = new JsonObject { ["scope"] = grant["scope"]!.DeepClone(), ["stackItemId"] = grant["stackItemId"]!.DeepClone(),
            ["targetObjectId"] = grant["targetObjectId"]!.DeepClone(), ["targetGeneration"] = grant["targetGeneration"]!.DeepClone(),
            ["appliedOrder"] = power["appliedOrder"]!.DeepClone() };
        var id = "BATTLE_LIMITED_POWER:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToJsonString(Json)))).ToLowerInvariant();
        grant["powerEffectId"] = id; power["effectId"] = id;
    }
    private static void EarlierBirth(JsonObject state)
    {
        var globals = state["untilEndOfTurnEffects"]!.AsArray();
        for (var i = 0; i < globals.Count; i++)
        {
            var text = globals[i]!.GetValue<string>();
            if (text.StartsWith(ScopePrefix))
            {
                var scope = JsonNode.Parse(text[ScopePrefix.Length..])!.AsObject();
                scope["startedAtTick"] = scope["startedAtTick"]!.GetValue<long>() - 1; globals[i] = ScopePrefix + scope.ToJsonString(Json);
            }
            else if (text.StartsWith(RolePrefix))
            {
                var role = JsonNode.Parse(text[RolePrefix.Length..])!.AsObject(); var tick = role["scope"]!["startedAtTick"]!.GetValue<long>();
                role["scope"]!["startedAtTick"] = tick - 1;
                if (role["acquiredAtTick"]!.GetValue<long>() == tick) role["acquiredAtTick"] = tick - 1;
                globals[i] = RolePrefix + role.ToJsonString(Json);
            }
        }
        var effects = Effects(state);
        for (var i = 0; i < effects.Count; i++)
        {
            var text = effects[i]!.GetValue<string>(); if (!text.StartsWith(GrantPrefix)) continue;
            var grant = JsonNode.Parse(text[GrantPrefix.Length..])!.AsObject();
            grant["scope"]!["startedAtTick"] = grant["scope"]!["startedAtTick"]!.GetValue<long>() - 1;
            RebindPower(state, grant); effects[i] = GrantPrefix + grant.ToJsonString(Json);
        }
    }

    internal static async Task<MatchState> ActualCopiedRoleState() => (await Actual.Value).State;

    private static async Task<Fixture> Build()
    {
        var initial = Position(); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funding=Enumerable.Range(1,20).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funding[0],funding.Take(12).ToArray()));await run.Act("P2",new TapRuneCommand(funding[12],funding.Skip(12).ToArray()));
        await run.Act("P2",new RecycleRuneCommand("ORANGE"));
        var greens=Enumerable.Range(1,4).Select(i=>"GREEN-"+i).ToArray();await run.Act("P2",new RecycleRuneCommand(greens[0],greens));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));
        foreach(var id in new[]{"SONG-1","SONG-2"}) {
            await run.Act("P2",new PlayCardCommand(id,"SFD·059/221",[],Destination:"BASE"));
            await run.Act("P2",new AssembleEquipmentCommand(id,"ATTACKER",["ASSEMBLE_GREEN"]));await run.Top();
            var origin=Assert.IsType<AttachedRuleTextState>(run.State.CardObjects[id].AttachedRuleText);
            Assert.Equal("SFD·110/221",origin.CardNo);Assert.Equal("ATTACKER",origin.Unit.ObjectId);Assert.Equal(1,origin.Revision);
        }
        await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));
        var earned=run.State.TriggerQueue.Select(t=>t.FieldContext).Concat(run.State.StackItems.Select(i=>i.FieldContext)).Where(c=>c is not null).ToArray();
        Assert.Equal(3,earned.Length);Assert.Single(earned,c=>c!.Origin is null);
        Assert.Equal(new[]{"SONG-1","SONG-2"},earned.Where(c=>c!.Origin is not null).Select(c=>c!.Origin!.Equipment.ObjectId).Order().ToArray());
        if(run.State.TriggerQueue.Count>0)await run.Act("P2",new OrderTriggersCommand(OrderedTriggerIds:run.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
        var checkpoint = run.State; var checkpointCommands = run.Commands(); var checkpointEvents = run.Events();
        foreach(var expected in new[]{6,12,24}) {await run.Top();Assert.Equal(expected,run.State.CardObjects["ATTACKER"].Power);}
        var ledger=run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers;
        Assert.Equal(new[]{3,6,12},ledger.Select(p=>p.SnapshotMight!.Value).ToArray());
        Assert.Equal(3,ledger.Select(p=>p.EffectId).Distinct().Count());Assert.All(ledger,p=>Assert.Equal("UNTIL_END_OF_BATTLE",p.Duration));
        await run.Replay();
        return new Fixture(initial, run.State, run.Commands(), run.Events(), checkpoint, checkpointCommands, checkpointEvents);
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects);
        var zones=new Dictionary<string,PlayerZones>(state.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        cards["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2");
        void Put(string id,string no,string zone){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,"P2");locations[id]=new("P2",zone);zones["P2"]=zone=="BASE"?zones["P2"] with {Base=[..zones["P2"].Base,id]}:zones["P2"] with {Hand=[..zones["P2"].Hand,id]};}
        foreach(var i in Enumerable.Range(13,8))Put("P2-FUND-"+i,"UNL-R06","BASE");foreach(var i in Enumerable.Range(1,4))Put("GREEN-"+i,"UNL-R02","BASE");
        Put("ORANGE","UNL-R04","BASE");Put("RED","UNL-R01","BASE");Put("SONG-1","SFD·059/221","HAND");Put("SONG-2","SFD·059/221","HAND");Put("DETACH","SFD·011/221","HAND");
        return state with {CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
