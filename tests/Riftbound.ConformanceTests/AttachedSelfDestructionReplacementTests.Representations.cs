using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    // Representation-only boundary. The current CN catalog has no verified
    // reachable equipment-animation consumer. This does not prove that transition.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RepresentationOnlyDetachedUnitGuardianUsesTheExistingUnitDestructionAndHourglassFlow(bool hourglass)
    {
        var initial = Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ANGEL"] = initial.CardObjects["ANGEL"] with {
                Tags = [CardObjectTags.EquipmentCard, CardObjectTags.UnitCard], Power = 3, ObjectGeneration = 1, ControllerId = "P1" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["ANGEL"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Hand = ["HOST"], Base = ["ANGEL"] } } };
        if (hourglass) initial = AddInitial(initial, "ZHONYA", "OGN·077/298", "P1");
        var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BASE"));
        if (hourglass) await run.Act("P1", new PlayCardCommand("ZHONYA", "OGN·077/298", [], Destination: "BASE"));
        Assert.Null(run.State.CardObjects["ANGEL"].AttachedToObjectId);
        await FundYellow(run, 2); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["ANGEL"])); await run.Top();
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains("HOST", run.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
        if (hourglass) {
            Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Base); Assert.Contains("ZHONYA", run.State.PlayerZones["P1"].Graveyard);
            Assert.True(run.State.CardObjects["ANGEL"].IsExhausted); Assert.Equal(0, run.State.CardObjects["ANGEL"].Damage);
            Assert.Contains(CardObjectTags.UnitCard, run.State.CardObjects["ANGEL"].Tags); Assert.Equal(1, run.State.CardObjects["ANGEL"].ObjectGeneration);
            Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "ANGEL");
        }
        else {
            Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
            Assert.DoesNotContain(CardObjectTags.UnitCard, run.State.CardObjects["ANGEL"].Tags);
            Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "ANGEL");
        }
        await run.Replay(); Export(initial, run, false, "representation-only-detached-unit-hourglass-" + hourglass);
    }

    [Fact]
    public async Task AnIllegalAttachedUnitRepresentationCannotReplaceTheUnchangedOriginalActionLog()
    {
        var (initial, run) = await OpenTwoGuardians(); var pending = run.State.PendingRuleChoice!;
        var changed = run.State.CardObjects["ANGEL"] with { Tags = [..run.State.CardObjects["ANGEL"].Tags, CardObjectTags.UnitCard] };
        var forged = run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects) { ["ANGEL"] = changed },
            PendingRuleChoice = pending with { Origin = pending.Origin with {
                CardObjects = new Dictionary<string, CardObjectState>(pending.Origin.CardObjects) { ["ANGEL"] = changed } } } };
        var frame = GuardianFrame(initial, run, forged);
        var originalCommands = JsonSerializer.Serialize(run.Commands(), Json);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch); Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C9_ATTACHED_REPLACEMENT_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "representation-only-illegal-attached-unit.json"), JsonSerializer.Serialize(new {
                scope = "Forged representation only; original action-log rejection, not a reachable animation or a structural unit-detach guard.",
                currentShapeErrors = GuardianErrors(frame), frame, replay, originalHistoryNeverRewritten = true }, Json));
        }
        await Choose(run, "ATTACHED_GEAR:HOST:ANGEL-2"); AssertHostRecalled(run, "P1"); await run.Replay();
    }
}
