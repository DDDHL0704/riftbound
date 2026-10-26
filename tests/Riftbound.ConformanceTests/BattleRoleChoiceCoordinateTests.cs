using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class BattleRoleChoiceCoordinateTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string ScopePrefix = "BATTLE_LIMITED_SCOPE:";
    private const string RolePrefix = "BATTLE_ROLE_FIRST:";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalAnswerCoordinatesSurviveJsonColdDuplicateAndOriginalReplay(bool gold)
    {
        var run = new Run(FinitePosition(gold, false));
        await PaidMove(run, gold);
        AssertRoles(run.Frame(), "BATTLE_ROLES_ESTABLISHED", 3);
        var earned = Markers(run.State);
        await run.Act("P1", new SurrenderCommand());
        Assert.Equal(earned, Markers(run.State));
        Assert.Equal(MatchStatuses.Finished, run.State.Status);
        run.Export("finite-final-" + gold);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACompletedResourceChoiceKeepsAnAlreadyAcceptedBattleIdentity(bool gold)
    {
        var initial = FinitePosition(gold, true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", "OGN·268/298", "P1") } };
        var run = new Run(initial);
        await run.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2"));
        var previous = Markers(run.State);
        AssertRoles(run.Frame(), "BATTLE_ROLES_ESTABLISHED", 2);
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·268/298", ["FIELD2"]));
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var request = run.State.PendingRuleChoice!.Request;
        await run.Decide(gold ? request.Options.Single(o => o.Id.StartsWith("RESOURCE:GOLD:", StringComparison.Ordinal)).Id : "RECYCLE_RUNE:RUNE");
        await run.Decide("SPEND_POWER:0");
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(previous, Markers(run.State));
        Assert.DoesNotContain(run.Frame().Events, e => e.Event.Kind == "BATTLE_ROLE_ACQUIRED");
        run.Export("existing-battle-" + gold);
    }

    [Theory]
    [InlineData("tick")]
    [InlineData("order")]
    public async Task CoherentlyForgedRoleCoordinatesCannotBorrowAnotherAcceptedEvent(string coordinate)
    {
        var run = new Run(FinitePosition(true, false));
        await PaidMove(run, true);
        var original = MatchStateHasher.Hash(run.State);
        var node = JsonSerializer.SerializeToNode(run.State, Web)!;
        var effects = node["untilEndOfTurnEffects"]!.AsArray();
        for (var i = 0; i < effects.Count; i++) {
            var text = effects[i]!.GetValue<string>();
            if (!text.StartsWith(ScopePrefix) && !text.StartsWith(RolePrefix)) continue;
            var prefix = text.StartsWith(ScopePrefix) ? ScopePrefix : RolePrefix;
            var marker = JsonNode.Parse(text[prefix.Length..])!;
            var scope = prefix == ScopePrefix ? marker : marker["scope"]!;
            if (coordinate == "tick") {
                scope["startedAtTick"] = 1;
                if (prefix == RolePrefix) marker["acquiredAtTick"] = 1;
            }
            else {
                scope["roleEventOrder"] = 0;
                if (prefix == RolePrefix) marker["roleEventOrder"] = 0;
            }
            effects[i] = prefix + marker.ToJsonString(Web);
        }
        var forged = node.Deserialize<MatchState>(Web)!;
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
        var frame = run.Frame(forged);
        Assert.Contains("battle scope has no matching accepted initial role event", frame.ValidationErrors);
        Assert.Equal(3, frame.ValidationErrors.Count(error => error == "battle first-role memo has no matching accepted acquisition event"));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(run.Initial!, frame.Commands, forged,
            new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
        Assert.Equal(original, MatchStateHasher.Hash(run.State));
        run.Export("forgery-" + coordinate, frame);
    }

    [Fact]
    public async Task EmptyRoomQaBirthKeepsPaidRolesAcrossColdRegistryRecovery()
    {
        // An accepted QA seed has original room birth; it is not a legal-deck opening.
        var run = new Run("role-coordinates-original-qa-birth");
        await run.Session.EnsurePlayerAsync("P1", default);
        await run.Cold();
        await run.Session.EnsurePlayerAsync("P2", default);
        await run.Cold();
        const string scenario = "imposed-movement-standard-pay";
        var seeded = await run.Session.SeedScenarioAsync("P1", "role-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        await run.Cold();
        await run.Act("P1", new MoveUnitCommand("QA-A", "BASE", "BATTLEFIELD:QA-FIELD2", SourceObjectIds: ["QA-A", "QA-B"]));
        await run.Decide("RECYCLE_RUNE:QA-RUNE");
        await run.Decide("PAY");
        AssertRoles(run.Frame(), "BATTLE_ROLES_ESTABLISHED", 3);
        var earned = Markers(run.State);
        await run.Act("P1", new SurrenderCommand());
        Assert.Equal(earned, Markers(run.State));
        run.Export("original-qa-birth-final");
    }

    [Fact]
    public async Task RetainedRoleCoordinatesBindAFollowingBattlefieldSkillGrant()
    {
        var initial = FinitePosition(true, false);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["FIELD2"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD2", "OGN·279/298", "P2") with { ControllerId = "P2" } } };
        var run = new Run(initial);
        await PaidMove(run, true);
        AssertRoles(run.Frame(), "BATTLE_ROLES_ESTABLISHED", 4);
        var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        Assert.Contains("PATROL", choice.LegalObjectIds);
        await run.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PATROL"]));
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var grantText = Assert.Single(run.State.CardObjects["PATROL"].UntilEndOfTurnEffects,
            text => text.StartsWith("BATTLE_LIMITED_GRANT:", StringComparison.Ordinal));
        var grant = JsonNode.Parse(grantText["BATTLE_LIMITED_GRANT:".Length..])!;
        var scopeText = Assert.Single(Markers(run.State), text => text.StartsWith(ScopePrefix));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(scopeText[ScopePrefix.Length..]), grant["scope"]));
        Assert.Contains(run.State.CardObjects["PATROL"].UntilEndOfTurnEffects, text => text.StartsWith("COMBAT_KEYWORD:坚守2:"));
        run.Export("paid-battlefield-skill-grant");
    }

    private static MatchState FinitePosition(bool gold, bool effect)
    {
        var state = Position(effect ? "effect" : "standard");
        return gold ? AddBase(state, "GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlCardNo) : state;
    }
    private static async Task PaidMove(Run run, bool gold)
    {
        await run.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        await ResourceAndPay(run, gold);
    }
    private static async Task ResourceAndPay(Run run, bool gold)
    {
        var request = run.State.PendingRuleChoice!.Request;
        await run.Decide(gold ? request.Options.Single(o => o.Id.StartsWith("RESOURCE:GOLD:", StringComparison.Ordinal)).Id : "RECYCLE_RUNE:RUNE");
        await run.Decide("PAY");
    }
    private static string[] Markers(MatchState state) => state.UntilEndOfTurnEffects
        .Where(text => text.StartsWith(ScopePrefix) || text.StartsWith(RolePrefix)).ToArray();
    private static void AssertRoles(MatchRecoveryFrame frame, string kind, int count)
    {
        var actual = Assert.Single(frame.Events, e => e.Event.Kind == kind);
        Assert.Equal(frame.CurrentTick, actual.Tick);
        var markers = Markers(frame.AuthoritativeState!);
        var scope = JsonNode.Parse(Assert.Single(markers, text => text.StartsWith(ScopePrefix))[ScopePrefix.Length..])!;
        Assert.Equal(actual.Tick, scope["startedAtTick"]!.GetValue<long>());
        Assert.Equal(actual.Order, scope["roleEventOrder"]!.GetValue<int>());
        var roles = markers.Where(text => text.StartsWith(RolePrefix)).Select(text => JsonNode.Parse(text[RolePrefix.Length..])!).ToArray();
        Assert.Equal(count, roles.Length);
        Assert.All(roles, role => {
            Assert.Equal(actual.Tick, role["acquiredAtTick"]!.GetValue<long>());
            Assert.Equal(actual.Order, role["roleEventOrder"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(scope, role["scope"]));
        });
    }

    private sealed class Run : IMatchJournal
    {
        private readonly List<MatchJournalEntry> entries = [];
        internal readonly MatchState? Initial;
        internal IMatchSession Session;
        private readonly bool fullBirth;
        internal MatchState State => entries.Count == 0 ? Initial! : entries[^1].AuthoritativeState;
        internal Run(MatchState initial) { Initial = initial; Session = new MatchSession(initial, new CoreRuleEngine(), this); }
        internal Run(string room) { fullBirth = true; Session = new MatchSession(room, new CoreRuleEngine(), this); }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal Task Decide(string option) => Act(State.PendingRuleChoice!.Request.PlayerId,
            new PayCostCommand(State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", [option]));
        internal async Task Act(string player, GameCommand command)
        {
            var id = "role-command-" + entries.Count;
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!;
            node["promptId"] = Session.PromptFor(player).PromptId; node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var result = await Session.SubmitAsync(player, id, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            await Cold();
            var count = entries.Count;
            var duplicate = await Session.SubmitAsync(player, id, command, raw, default);
            Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count);
            if (MatchStateHasher.Hash(result.State) != MatchStateHasher.Hash(duplicate.State)
                && Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is { Length: > 0 } folder)
                File.WriteAllText(Path.Combine(folder, "duplicate-state-mismatch-" + id + ".json"),
                    JsonSerializer.Serialize(new { raw, original = result.State, restored = duplicate.State, frame = Frame() }, Web));
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
        }
        internal MatchRecoveryFrame Frame(MatchState? state = null)
        {
            state ??= State; var sequence = entries[^1].CompletedEventSequence;
            var commands = entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            var initial = Initial ?? MatchState.Create(state.RoomId) with { Seed = entries[0].OpeningSeed };
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
        internal async Task Cold()
        {
            var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), Web), Web)!;
            Assert.Empty(frame.ValidationErrors);
            if (fullBirth) {
                Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
                var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new Store(frame));
                Session = await registry.GetOrCreateAsync(frame.RoomId, default);
            }
            else {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(Initial!, frame.Commands, State, new CoreRuleEngine(), default, frame.Events);
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
                Session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            }
        }
        internal void Export(string name, MatchRecoveryFrame? attack = null)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new { original = Frame(), attack,
                fullBirth, scope = fullBirth ? "Actual room seats and accepted QA seed; no ordinary legal-deck opening or external services."
                    : "Explicit finite position; actual commands, JSON cold restore, duplicate and original event replay." }, Web));
        }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
