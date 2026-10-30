using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZaunCapturedSourceContinuationTests
{
    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 3)]
    [InlineData("P2", 0)] [InlineData("P2", 3)]
    public async Task ReplacedBattlefieldKeepsCapturedZaunIdentityThroughThePrivateHandChoice(string player, int generation)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·298/298");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = initial.CardObjects["BF"] with { ObjectGeneration = generation },
                ["LEGEND"] = new("LEGEND", cardNo: "UNL-195/219", ownerId: player, controllerId: player) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new(player, "LEGEND") } };
        var directory = Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            directory is null ? null : Path.Combine(directory, $"zaun-replaced-{player}-{generation}"));
        try
        {
            await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await driver.Act(player, new PassFocusCommand());
            await driver.Act(ThreeBattlefieldTimingProbe.Other(player), new PassFocusCommand());
            var ivern = Assert.Single(driver.State.TriggerQueue, t => t.EffectKind == "HOLD_IVERN");
            var zaun = Assert.Single(driver.State.TriggerQueue, t => t.EffectKind == TriggerKinds.BattlefieldConquerDiscardDraw);
            await driver.Act(player, new OrderTriggersCommand(OrderedTriggerIds: [ivern.TriggerId, zaun.TriggerId]));
            var confirm = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act(player, new ChooseCardsCommand(confirm.ChoiceId, confirm.ChoiceWindow, ["LEGEND"]));
            var replaced = await driver.Top();
            Assert.Contains(replaced.Events, e => e.Kind == "BATTLEFIELD_REPLACED");
            Assert.Equal("UNL·T03", driver.State.CardObjects["BF"].CardNo);
            Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerDiscardDraw);
            await driver.Top();
            var choice = Assert.IsType<PendingHandChoiceState>(driver.State.PendingHandChoice);
            Assert.Contains("H2", choice.LegalObjectIds);
            var continuation = Assert.IsType<BattlefieldConquestDiscardDrawContinuation>(choice.BattlefieldConquestDiscardDraw);
            Assert.Equal(generation, continuation.Context.Source.Generation);
            var forgedChoice = choice with { BattlefieldConquestDiscardDraw = continuation with {
                Context = continuation.Context with { Source = new("BF", generation == 0 ? 1 : 0) } } };
            var forged = driver.State with { PendingHandChoice = forgedChoice };
            var sequence = driver.Commands().Last().CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(forged);
            var prompts = ResolutionResult.BuildPrompts(forged);
            var views = forged.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
                forged.Tick, sequence, snapshots[id], forged.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
            Assert.NotEmpty(MatchRecoveryValidator.Validate(forged.RoomId, sequence, driver.Commands(), driver.Events(),
                views, forged, forged.Tick, spectator));
            var resolved = await driver.Act(player, new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]));
            Assert.Contains("H2", driver.State.PlayerZones[player].Graveyard);
            Assert.Contains("DRAW1", driver.State.PlayerZones[player].Hand);
            var reported = Assert.Single(resolved.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                && Equals(e.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerDiscardDraw));
            Assert.Equal("OGN·298/298", reported.Payload.GetValueOrDefault("battlefieldCardNo"));
            await RideTheWindCurrentMovementTests.Replay(initial, driver);
        }
        finally { driver.Export("captured-source-summary.json", new { initial, state = driver.State, commands = driver.Commands(), events = driver.Events() }); }
    }
}
