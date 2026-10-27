using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit midgame field fixtures; every tested resource is acquired by real commands.
public sealed partial class ImposedPowerAllocationTests
{
    private const string Allocate = "ALLOCATE_IMPOSED_MOVEMENT_POWER";
    private const string Confirm = "CONFIRM_IMPOSED_MOVEMENT_POWER";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, 1, 0)] [InlineData(false, 0, 1)]
    [InlineData(true, 1, 0)] [InlineData(true, 0, 1)]
    public async Task EarnedBlueAndRedCanPayTheSameImposedFeeWithEitherLegalVector(bool effect, int blue, int red)
    {
        var run = await Open(effect, "vector-" + effect + "-" + blue + "-" + red);
        await run.Cold("before-allocation");
        run.Save("actual-before-allocation", new { run.State, workingPool = run.Pool(), expectedBlue = blue, expectedRed = red });
        var pending = run.State.PendingRuleChoice;
        Assert.NotNull(pending);
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", pending.Request.Kind);
        Assert.Contains(pending.Request.Options, option => option.Id == Allocate);
        Assert.All(effect ? new[] { "D", "D2" } : new[] { "A", "B" }, id => {
            Assert.Equal(effect ? "FIELD1" : null, run.State.ObjectLocations[id].BattlefieldObjectId);
            if (!effect) Assert.False(run.State.CardObjects[id].IsExhausted);
        });
        await run.Choice(Allocate);
        for (var step = 0; ; step++)
        {
            Assert.InRange(step, 0, 217);
            await run.Cold("allocation-" + step);
            Assert.Equal(1, run.Pool().PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
            Assert.Equal(1, run.Pool().PowerByTrait.GetValueOrDefault(RuneTrait.Red));
            var request = run.State.PendingRuleChoice!.Request;
            Assert.Equal(1, request.PowerCost);
            if (request.Options.Any(o => o.Id == Confirm)) break;
            var candidates = request.Options.Where(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, candidates.Length);
            var purpose = Uri.UnescapeDataString(candidates[0].Id.Split(':')[4]);
            var desired = purpose.EndsWith("TRAIT:blue", StringComparison.Ordinal) ? blue
                : purpose.EndsWith("TRAIT:red", StringComparison.Ordinal) ? red : 0;
            var choice = Assert.Single(candidates, o => {
                var parts = o.Id.Split(':'); return int.Parse(parts[^2]) <= desired && desired <= int.Parse(parts[^1]);
            });
            Assert.DoesNotContain("SPELL", choice.Id, StringComparison.Ordinal);
            await run.Choice(choice.Id);
        }
        await run.Choice(Confirm);
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(1 - blue, run.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
        Assert.Equal(1 - red, run.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        var receipt = Assert.Single(run.Entries.SelectMany(e => e.Events), IsImposedCost);
        var payload = JsonSerializer.SerializeToElement(receipt.Payload, Web);
        Assert.Equal(1, payload.GetProperty("totalPowerCost").GetInt32());
        Assert.Equal(0, payload.GetProperty("genericPower").GetInt32());
        var typed = payload.GetProperty("powerByTrait");
        Assert.Equal(blue, typed.TryGetProperty("blue", out var b) ? b.GetInt32() : 0);
        Assert.Equal(red, typed.TryGetProperty("red", out var r) ? r.GetInt32() : 0);
        Assert.All(effect ? new[] { "D", "D2" } : new[] { "A", "B" }, id => Assert.Equal("FIELD2", run.State.ObjectLocations[id].BattlefieldObjectId));
        await run.Cold("final");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UniquelyFundedStandardGroupStillCompletesAsOneIntent(bool payAll)
    {
        var run = new Run(Position("unique-" + payAll, payAll, singleTrait: !payAll));
        await run.Act("gain-blue", "P1", new RecycleRuneCommand("BLUE"));
        await run.Act("gain-second", "P1", new RecycleRuneCommand("RED"));
        var before = run.Entries.Count;
        await run.Act("move", "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        Assert.Equal(before + 1, run.Entries.Count);
        Assert.Null(run.State.PendingRuleChoice);
        Assert.All(new[] { "A", "B" }, id => Assert.Equal("FIELD2", run.State.ObjectLocations[id].BattlefieldObjectId));
        Assert.Equal(payAll ? 0 : 1, run.State.RunePools["P1"].TotalPower);
        var cost = Assert.Single(run.Entries[^1].Events, IsImposedCost);
        Assert.Equal(payAll ? 2 : 1, JsonSerializer.SerializeToElement(cost.Payload, Web).GetProperty("totalPowerCost").GetInt32());
        await run.Cold("unique-final");
    }

    private static async Task<Run> Open(bool effect, string name)
    {
        var run = new Run(Position(name));
        if (effect)
        {
            foreach (var rune in new[] { "BLUE", "RED", "GREEN", "EXTRA" }) await run.Act("tap-" + rune, "P1", new TapRuneCommand(rune));
            await run.Act("green-for-spell", "P1", new RecycleRuneCommand("GREEN"));
            await run.Act("spell", "P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"));
            Assert.Equal(0, run.State.RunePools["P1"].Mana); Assert.Equal(0, run.State.RunePools["P1"].TotalPower);
            await run.Act("pass-1", run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act("pass-2", run.State.PriorityPlayerId!, new PassPriorityCommand());
            foreach (var rune in new[] { "BLUE", "RED" }) await run.Choice("RECYCLE_RUNE:" + rune);
        }
        else
        {
            await run.Act("gain-blue", "P1", new RecycleRuneCommand("BLUE"));
            await run.Act("gain-red", "P1", new RecycleRuneCommand("RED"));
            await run.Act("move", "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        }
        return run;
    }

    private static bool IsImposedCost(GameEvent e)
        => e.Kind == "COST_PAID" && Equals(e.Payload.GetValueOrDefault("paymentWindow"), "IMPOSED_MOVEMENT_POWER_PAYMENT");

    private static MatchState Position(string name, bool secondPatrol = false, bool singleTrait = false)
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["A"] = Printed("A", "SFD·125/221", "P1"), ["B"] = Printed("B", "SFD·125/221", "P1"),
            ["BLUE"] = Printed("BLUE", "OGN·089/298", "P1"), ["RED"] = Printed("RED", singleTrait ? "OGN·089/298" : "OGN·007/298", "P1"),
            ["GREEN"] = Printed("GREEN", "OGN·042/298", "P1"), ["EXTRA"] = Printed("EXTRA", "OGN·042/298", "P1"),
            ["SPELL"] = Printed("SPELL", "UNL-054/219", "P1"),
            ["D"] = Printed("D", "SFD·125/221", "P2"), ["D2"] = Printed("D2", "SFD·125/221", "P2"),
            ["PATROL"] = Printed("PATROL", "UNL-163/219", "P2"),
            ["FIELD1"] = Printed("FIELD1", "OGN·278/298", "P2") with { ControllerId = "P2" },
            ["FIELD2"] = Printed("FIELD2", "OGN·287/298", "P2") with { ControllerId = "P2" }
        };
        var locations = cards.Keys.ToDictionary(id => id, id => id switch {
            "SPELL" => new ObjectLocationState("P1", "HAND"),
            "D" or "D2" or "FIELD1" => new("P2", "BATTLEFIELD", "FIELD1"),
            "PATROL" or "FIELD2" => new("P2", "BATTLEFIELD", "FIELD2"),
            _ => new("P1", "BASE")
        });
        var opponents = new[] { "FIELD1", "D", "D2", "FIELD2", "PATROL" };
        if (secondPatrol)
        {
            cards["PATROL2"] = Printed("PATROL2", "UNL-163/219", "P2"); locations["PATROL2"] = new("P2", "BATTLEFIELD", "FIELD2");
            opponents = [.. opponents, "PATROL2"];
        }
        return new(name, 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["A", "B", "BLUE", "RED", "GREEN", "EXTRA"], Hand = ["SPELL"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = opponents } }, cardObjects: cards, objectLocations: locations);
    }

    private static CardObjectState Printed(string id, string cardNo, string owner)
    {
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = [id, owner, cardNo, null];
        Assert.True((bool)factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!);
        return Assert.IsType<CardObjectState>(args[3]);
    }

    private sealed class Run : IMatchJournal
    {
        private readonly MatchState initial;
        private MatchSession? session;
        private int artifacts;
        private int intents;
        public MatchState Initial => initial;
        public Input? LastInput { get; private set; }
        public sealed record Input(string Actor, string Intent, JsonElement Raw);
        public MatchState State { get; private set; }
        public Run(MatchState initial) { this.initial = initial; State = initial; }
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
        public RunePool Pool() => (State.PendingRuleChoice?.Request.Preview ?? State).RunePools["P1"];
        public async Task Act(string intent, string actor, GameCommand command)
        {
            var wire = Wire(command, actor);
            var result = await Repeat(actor, initial.RoomId + "-" + intent, wire);
            Save("step-" + Entries.Count, new { actor, intent, wire, result });
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        public JsonElement Wire(GameCommand command, string actor = "P1")
        {
            session ??= new(State, new CoreRuleEngine(), this);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            return JsonSerializer.SerializeToElement(raw);
        }
        public Task<ResolutionResult> Submit(GameCommand command, string actor = "P1")
            => Repeat(actor, initial.RoomId + "-input-" + intents++, Wire(command, actor));
        public async Task<ResolutionResult> Repeat(string actor, string intent, JsonElement wire)
        {
            session ??= new(State, new CoreRuleEngine(), this);
            LastInput = new(actor, intent, wire);
            var result = await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(wire), wire, default);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (result.Accepted) State = result.State;
            return result;
        }
        public Task Choice(string id)
        {
            var request = State.PendingRuleChoice!.Request;
            return Act("choice-" + Entries.Count, request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]));
        }
        public MatchRecoveryFrame Frame(MatchState? authority = null)
        {
            var state = authority ?? State;
            var commands = Entries.Select(entry => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(entry, Web), Web)!).ToArray();
            var events = Entries.SelectMany(entry => entry.Events.Select((e, i) => new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, e))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state); var last = Entries[^1];
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, last.CompletedEventSequence,
                snapshots[p], state.Tick, last.CompletedEventSequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, last.CompletedEventSequence, events.Select(e => e.Event).ToArray(), state);
            return new MatchRecoveryFrame(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events, views, [], state, initial, spectator)
                { OpeningSeed = Entries[0].OpeningSeed };
        }
        public async Task Cold(string label)
        {
            var loaded = DecodeAndValidate(Frame());
            Save(label + "-decoded-frame", loaded); Assert.Empty(loaded.ValidationErrors);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, loaded.Commands, State, new CoreRuleEngine(), default, loaded.Events);
            Save(label + "-replay", replay); Assert.True(replay.IsMatch, string.Join(";", replay.Errors));
            session = MatchSession.Restore(loaded, new CoreRuleEngine(), this); State = loaded.AuthoritativeState!;
            foreach (var player in State.Seats.Keys)
            {
                Assert.Equal(ViewHash(ResolutionResult.BuildSnapshots(State)[player]), ViewHash(session.SnapshotFor(player)));
                Assert.Equal(ViewHash(ResolutionResult.BuildPrompts(State)[player]), ViewHash(session.PromptFor(player)));
            }
            var count = Entries.Count; var hash = MatchStateHasher.Hash(State);
            var last = Entries[^1];
            var duplicate = await session.SubmitAsync(last.PlayerId, last.ClientIntentId, GameCommandJsonMapper.Map(last.RawCommand!.Value), last.RawCommand, default);
            Assert.Equal(last.Accepted, duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, Entries.Count);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
        }
        public void Save(string name, object value)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_ALLOCATION_EVIDENCE") is not { Length: > 0 } root) return;
            var folder = Path.Combine(root, initial.RoomId); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, artifacts++ + "-" + name + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
            var assemblies = new[] { typeof(ImposedPowerAllocationTests).Assembly, typeof(CoreRuleEngine).Assembly,
                typeof(GameCommand).Assembly, typeof(OfficialCardCatalog).Assembly }.Select(a => new { name = a.GetName().Name,
                    path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                    informationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) });
            File.WriteAllText(Path.Combine(folder, "runtime.json"), JsonSerializer.Serialize(new { assemblies, ruleInputs = RuleInputIdentity.Current }, Web));
        }
    }
}
