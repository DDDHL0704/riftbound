using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredChosenActionReplacementTests
{
    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task CapturedTargetReplacementForgeriesKeepTheOriginalJournalAndFailTheirRequiredRecoveryLayer(string no)
    {
        var run = new Driver(Position(no, "P1", "OGN·050/298"), "witness-forgeries-" + no);
        await PlayAndEmpower(run, no);
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·050/298", ["UNIT"])); await run.Top();
        var state = run.State; var unit = state.CardObjects["UNIT"];
        var modifier = Assert.Single(unit.UntilEndOfTurnPowerModifiers);
        var context = Assert.IsType<ChosenActionReplacementContext>(modifier.ChosenActionReplacement);
        var step = Assert.Single(context.Steps);
        var cases = new (string Kind, PowerModifierLedgerEntry Modifier, bool ShapeRejects)[] {
            ("missing-witness", modifier with { ChosenActionReplacement = null }, true),
            ("changed-public-effect-id", modifier with { EffectId = modifier.EffectId + ":FOREIGN" }, true),
            ("missing-controller", modifier with { ChosenActionReplacement = context with { EffectControllerId = null! } }, true),
            ("blank-stack", modifier with { ChosenActionReplacement = context with { StackItemId = "" } }, true),
            ("unchosen-action-kind", modifier with { ChosenActionReplacement = context with { OriginalKind = "GLOBAL_STUN" } }, true),
            ("future-target-generation", modifier with { ChosenActionReplacement = context with { Target = new("UNIT", 2) } }, true),
            ("wrong-printed-face", modifier with { SourceCardNo = "VEN·069", ChosenActionReplacement = context with {
                Steps = [step with { Rule = step.Rule with { CardNo = "VEN·069" } }] } }, true),
            ("unempowered-source", modifier with { ChosenActionReplacement = context with {
                Steps = [step with { Rule = step.Rule with { WasEmpowered = false } }] } }, true),
            ("duplicate-instance", modifier with { ChosenActionReplacement = context with { Steps = [step, step] } }, true),
            ("wrong-positive-amount", modifier with { PowerDelta = 4, RequestedPowerDelta = 4 }, true),
            ("invented-floor", modifier with { ChosenActionReplacement = context with { MinimumMight = 1 } }, true),
            // These individually plausible captures require the unchanged real
            // command history to prove who acted, when, and through which source.
            ("different-original-controller", modifier with { ChosenActionReplacement = context with { EffectControllerId = "P2" } }, false),
            ("different-original-source", modifier with { ChosenActionReplacement = context with { SourceObjectId = "NEVER-P2" } }, false),
            ("different-original-stack", modifier with { ChosenActionReplacement = context with { StackItemId = "FORGED-ORIGINAL-STACK" } }, false),
            ("different-rule-controller", modifier with { ChosenActionReplacement = context with {
                Steps = [step with { Rule = step.Rule with { ControllerId = "P2" } }] } }, false),
            ("different-capture-tick", modifier with { EffectId = "TARGETED-ACTION:1:0:UNIT:1:1",
                ChosenActionReplacement = context with { AppliedTick = 1 } }, false)
        };
        var originalCommands = JsonSerializer.Serialize(RecoveryFrame(run).Commands, Json);
        foreach (var item in cases) {
            var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = unit with { UntilEndOfTurnPowerModifiers = [item.Modifier] } } };
            var frame = RecoveryFrame(run, forged); var errors = RecoveryErrors(frame);
            if (item.ShapeRejects) Assert.Contains("invalid captured chosen action replacement", errors);
            else Assert.Empty(errors);
            await RejectOriginalBirth(run, frame, item.Kind, errors);
            Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));
        }
        var secondContext = context with { Occurrence = 1 };
        var extra = modifier with { AppliedOrder = 2, EffectivePower = 12, ResultingPower = 12,
            EffectId = $"TARGETED-ACTION:{context.AppliedTick}:1:UNIT:1:2", ChosenActionReplacement = secondContext };
        var extraState = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = unit with { Power = 12, UntilEndOfTurnPowerModifier = 6, UntilEndOfTurnPowerModifiers = [modifier, extra] } } };
        var extraFrame = RecoveryFrame(run, extraState); var extraErrors = RecoveryErrors(extraFrame);
        Assert.Empty(extraErrors);
        await RejectOriginalBirth(run, extraFrame, "coherent-extra-positive-application", extraErrors);
        Assert.Equal(originalCommands, JsonSerializer.Serialize(extraFrame.Commands, Json));
        await run.Cold();
    }

    [Fact]
    public async Task PublicMightyFrameTamperingCannotReplaceThePrivateWitnessOrItsOriginalBirth()
    {
        var initial = AddCard(Position("VEN·086", "P2", "OGN·050/298"), "SMOKE", "OGN·093/298", "P1", "HAND");
        initial = AddCard(initial, "OBSERVE-P1", "SFD·180/221", "P1", "BASE");
        var run = new Driver(initial, "mighty-frame-forgeries");
        await run.Act("P1", new PlayCardCommand("UNIT", "VEN·086", [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("SMOKE", "OGN·093/298", ["UNIT"])); await run.Top();
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        await OpenEnemyFocus(run);
        await run.Act("P2", new PlayCardCommand("SPELL", "OGN·050/298", ["UNIT"])); await run.Top();
        var state = run.State; var item = Assert.Single(state.StackItems, i => i.MightySkill is not null);
        var context = item.MightySkill!; var publicUnit = context.After.CardObjects["UNIT"];
        var witness = Assert.Single(state.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers, m => m.ChosenActionReplacement is not null).ChosenActionReplacement;
        var variants = new[] {
            (Kind: "private-witness-in-public-frame", Unit: publicUnit with {
                UntilEndOfTurnPowerModifiers = publicUnit.UntilEndOfTurnPowerModifiers.Select(m => m.EffectKind == "CHOSEN_ACTION_REPLACEMENT_MIGHT"
                    ? m with { ChosenActionReplacement = witness } : m).ToArray() }, AfterMight: 5),
            (Kind: "forged-public-frame-digits", Unit: publicUnit with { Power = 6 }, AfterMight: 6)
        };
        foreach (var variant in variants) {
            var changed = context with { AfterMight = variant.AfterMight, After = context.After with {
                CardObjects = new Dictionary<string, CardObjectState>(context.After.CardObjects) { ["UNIT"] = variant.Unit } } };
            var forgedState = state with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId
                ? i with { MightySkill = changed } : i).ToArray() };
            var coherent = RecoveryFrame(run, forgedState);
            var viewOnly = RecoveryFrame(run) with { PlayerViews = coherent.PlayerViews, SpectatorReplayFrame = coherent.SpectatorReplayFrame };
            Assert.Contains(RecoveryErrors(viewOnly), e => e.Contains("captured Mighty transition differs from authoritative state", StringComparison.Ordinal));
            await RejectOriginalBirth(run, coherent, variant.Kind, RecoveryErrors(coherent));
        }
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealChosenOnPlaySkillKeepsItsPrivateOriginAfterTheSourceUnitReturnsToHand(string no)
    {
        var initial = AddCard(AddCard(Position(no, "P1", "OGN·093/298"), "PRIVATE-SOURCE", "OGN·051/298", "P1", "HAND"),
            "HIDE", "OGN·104/298", "P1", "HAND");
        initial = AddCard(initial, "OBSERVE-P1", "SFD·180/221", "P1", "BASE");
        var run = new Driver(initial, "real-hidden-on-play-origin-" + no);
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·093/298", ["UNIT"])); await run.Top();
        Assert.Equal(2, run.State.CardObjects["UNIT"].Power);
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("PRIVATE-SOURCE", "OGN·051/298", ["UNIT"], Destination: "BASE"));
        if (run.State.PendingCardChoice is { } choice)
            await run.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        var ability = Assert.Single(run.State.StackItems);
        var generation = run.State.CardObjects["PRIVATE-SOURCE"].ObjectGeneration;
        await run.Act("P1", new PlayCardCommand("HIDE", "OGN·104/298", ["PRIVATE-SOURCE"])); await run.Top();
        Assert.Contains("PRIVATE-SOURCE", run.State.PlayerZones["P1"].Hand);
        Assert.True(run.State.CardObjects["PRIVATE-SOURCE"].ObjectGeneration > generation);
        Assert.Equal(ability.StackItemId, Assert.Single(run.State.StackItems).StackItemId);
        await run.Top();
        Assert.Equal(5, run.State.CardObjects["UNIT"].Power);
        var witness = Assert.Single(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers,
            m => m.ChosenActionReplacement is not null).ChosenActionReplacement!;
        Assert.Equal("PRIVATE-SOURCE", witness.SourceObjectId); Assert.Equal("OGN·051/298", witness.SourceCardNo);
        Assert.Equal(ability.StackItemId, witness.StackItemId);
        var mighty = Assert.Single(run.State.StackItems, i => i.MightySkill is not null).MightySkill!;
        Assert.Equal(2, mighty.BeforeMight); Assert.Equal(5, mighty.AfterMight); Assert.Equal("P1", mighty.ControllerId);
        Assert.DoesNotContain("PRIVATE-SOURCE", JsonSerializer.Serialize(mighty, Json));
        foreach (var view in ResolutionResult.BuildSnapshots(run.State).Values)
            Assert.DoesNotContain("PRIVATE-SOURCE", JsonSerializer.Serialize(new { view.Stack, view.Timing }, Json));
        Assert.DoesNotContain("\"PRIVATE-SOURCE\"", JsonSerializer.Serialize(new {
            snapshot = run.Session.SnapshotFor("P2"), prompt = run.Session.PromptFor("P2") }, Json));
        Assert.DoesNotContain("\"PRIVATE-SOURCE\"", JsonSerializer.Serialize(RecoveryFrame(run).SpectatorReplayFrame, Json));
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RepresentationOnlyPositiveOverflowRejectsAndTheSameRejectingSessionStillAcceptsSurrender(string no)
    {
        // Explicit registered-card representation QA, not a natural deck or an
        // assertion that a real play/Empower sequence reached this numeric value.
        var initial = Position(no, "P1", "OGN·050/298");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { Power = int.MaxValue - 1, IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Hand = ["SPELL"], Battlefields = [..initial.PlayerZones["P1"].Battlefields, "UNIT"] } }
        };
        var run = new Driver(initial, "representation-positive-overflow-" + no);
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·050/298", ["UNIT"]));
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var session = run.Session; var before = run.State; var count = run.Entries.Count;
        var rejected = await run.Reject(run.State.PriorityPlayerId!, new PassPriorityCommand(), cold: false);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
        Assert.Equal(before.RngCursor, rejected.State.RngCursor); Assert.Equal(before.PriorityPlayerId, rejected.State.PriorityPlayerId);
        Assert.Equal(before.ActivePlayerId, rejected.State.ActivePlayerId);
        Assert.Equal(JsonSerializer.Serialize(before.RunePools, Json), JsonSerializer.Serialize(rejected.State.RunePools, Json));
        Assert.Same(session, run.Session); Assert.Equal(count + 1, run.Entries.Count);
        await run.Act("P1", new SurrenderCommand(), cold: false);
        Assert.Same(session, run.Session); Assert.Equal(MatchStatuses.Finished, run.State.Status); Assert.Equal("P2", run.State.WinnerPlayerId);
        Assert.All(ResolutionResult.BuildPrompts(run.State).Values, p => Assert.False(p.Actionable));
        Assert.Single(run.Entries, e => !e.Accepted); Assert.Equal(CommandTypes.Surrender, run.Entries[^1].CommandType);
        await run.Cold();
    }

    private static MatchRecoveryFrame RecoveryFrame(Driver run, MatchState? authority = null)
    {
        var state = authority ?? run.State; var sequence = run.Entries[^1].CompletedEventSequence;
        var commands = run.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        var events = run.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, [], state, run.Initial, spectator) { OpeningSeed = run.Initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    }

    private static IReadOnlyList<string> RecoveryErrors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static async Task RejectOriginalBirth(Driver run, MatchRecoveryFrame frame, string kind, IReadOnlyList<string> errors)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(run.Initial, frame.Commands,
            frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
        run.Save("forgery-" + kind + ".json", new { kind, shapeErrors = errors, originalBirthReplay = replay,
            originalCommandsNeverRewritten = true, frame });
    }
}
