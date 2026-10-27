using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed class CeasedLastBreathSourceRecoveryTests
{
    private const string UnitFactory = "OGN·271/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task AFactoryShapedReceiptCannotReplaceTheActualAcceptedPhysicalSourceHistory()
    {
        var (initial, game, _) = await Flow.CaptureDraws();
        try
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(UnitFactory, out var unit)); Assert.Contains(CardObjectTags.UnitCard, unit.Tags);
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, out var gold)); Assert.DoesNotContain(CardObjectTags.UnitCard, gold.Tags);
            Assert.Equal(3, game.State.TriggerQueue.Count);
            var missingFactory = RemovePhysicalSource(game.State, null);
            Assert.NotEmpty(ViewErrors(missingFactory));
            foreach (var factory in new[] { "UNKNOWN-FACTORY", P4ActivatedAbilityCatalog.GoldTokenUnlCardNo })
                Assert.Contains(ViewErrors(RemovePhysicalSource(game.State, factory)), e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
            var queueForgery = RemovePhysicalSource(game.State, UnitFactory);
            Assert.Empty(ViewErrors(queueForgery)); // Structurally plausible is not accepted history.
            Assert.NotEmpty(ViewErrors(queueForgery with { TriggerQueue = queueForgery.TriggerQueue
                .Select(t => t with { LastBreathDraw = null }).ToArray() }));
            Assert.NotEmpty(ViewErrors(queueForgery with { TriggerQueue = queueForgery.TriggerQueue
                .Select(t => t with { LastBreathDraw = t.LastBreathDraw! with { Source = new("UNIT", -1) } }).ToArray() }));
            var queueViews = Views(queueForgery);
            var queue = JsonSerializer.SerializeToElement(queueViews["P2"].Snapshot.Timing["triggerQueue"], Json).EnumerateArray()
                .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), Json)!).ToArray();
            queue[0]["sourceObjectId"] = "NO-BIRTH-SOURCE";
            var view = queueViews["P2"]; queueViews["P2"] = view with { Snapshot = view.Snapshot with {
                Timing = new Dictionary<string, object?>(view.Snapshot.Timing) { ["triggerQueue"] = queue } } };
            Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(queueForgery, queueViews,
                Spectator(queueForgery)), e => e.Contains("NO-BIRTH-SOURCE", StringComparison.Ordinal) && e.Contains("missing", StringComparison.Ordinal));
            var queueAudit = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), queueForgery, new CoreRuleEngine(), default, game.Events());
            Assert.False(queueAudit.IsMatch); Assert.Contains(queueAudit.Errors, e => e.Contains("final state hash", StringComparison.Ordinal));

            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var stackForgery = RemovePhysicalSource(game.State, UnitFactory);
            Assert.Empty(ViewErrors(stackForgery));
            Assert.NotEmpty(ViewErrors(RemovePhysicalSource(game.State, null)));
            Assert.NotEmpty(ViewErrors(stackForgery with { StackItems = stackForgery.StackItems
                .Select(t => t with { LastBreathDraw = null }).ToArray() }));
            var spectator = Spectator(stackForgery);
            var stack = spectator.SpectatorSnapshot.Stack.Select(item =>
                JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(item, Json), Json)!).ToArray();
            stack[0]["sourceObjectId"] = "NO-BIRTH-SOURCE";
            spectator = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Stack = stack.Cast<object?>().ToArray() } };
            Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(stackForgery, Views(stackForgery), spectator),
                e => e.Contains("NO-BIRTH-SOURCE", StringComparison.Ordinal) && e.Contains("missing", StringComparison.Ordinal));
            var stackAudit = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), stackForgery, new CoreRuleEngine(), default, game.Events());
            Assert.False(stackAudit.IsMatch); Assert.Contains(stackAudit.Errors, e => e.Contains("final state hash", StringComparison.Ordinal));
            if (Environment.GetEnvironmentVariable("RIFTBOUND_CEASED_LAST_BREATH_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "physical-source-history-cannot-be-forged.json"), JsonSerializer.Serialize(new {
                    scope = "Negative evidence: a real paid physical source death is manually forged into a structurally plausible ceased-token receipt; original history must reject it. No manually forged state is accepted or played.",
                    queueForgery, stackForgery, queueAudit, stackAudit, commands = game.Commands(), events = game.Events()
                }, Json));
            }
            await game.Replay();
        }
        finally { await Flow.Save("physical-source-must-not-be-forged-as-ceased-token", initial, game); }
    }

    private static MatchState RemovePhysicalSource(MatchState state, string? factory)
        => state with {
            CardObjects = state.CardObjects.Where(p => p.Key != "UNIT").ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = state.ObjectLocations.Where(p => p.Key != "UNIT").ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with { Graveyard = p.Value.Graveyard.Where(id => id != "UNIT").ToArray() }),
            TriggerQueue = state.TriggerQueue.Select(t => t with { LastBreathDraw = t.LastBreathDraw! with { SourceTokenFactoryCardNo = factory } }).ToArray(),
            StackItems = state.StackItems.Select(t => t with { LastBreathDraw = t.LastBreathDraw! with { SourceTokenFactoryCardNo = factory } }).ToArray() };

    private static Dictionary<string, RecoveredPlayerView> Views(MatchState state)
    {
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        return state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, 0, snapshots[p], state.Tick, 0, prompts[p]));
    }
    private static MatchReplayFrame Spectator(MatchState state)
        => MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
    private static IReadOnlyList<string> ViewErrors(MatchState state)
        => MatchRecoveryValidator.ValidateComponentStateAndViews(state, Views(state), Spectator(state));
}
