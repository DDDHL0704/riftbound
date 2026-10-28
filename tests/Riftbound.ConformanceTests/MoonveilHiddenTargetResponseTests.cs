using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MoonveilHiddenTargetResponseTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ReturningTheConfirmedEquipmentToHandHidesItsIdentityAndDoesNotRetarget(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PULL"] = new("PULL", cardNo: "SFD·135/221", tags: [CardObjectTags.SpellCard], ownerId: player, controllerId: player),
                ["OWN-RUNE"] = new("OWN-RUNE", cardNo: "UNL-R04", tags: [CardObjectTags.RuneCard], ownerId: player, controllerId: player),
                ["BACKUP"] = new("BACKUP", cardNo: "SFD·019/221", isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard], ownerId: player, controllerId: player) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Hand = initial.PlayerZones[player].Hand.Append("PULL").ToArray(),
                    Base = initial.PlayerZones[player].Base.Concat(["OWN-RUNE", "BACKUP"]).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["PULL"] = new(player, "HAND"), ["OWN-RUNE"] = new(player, "BASE"), ["BACKUP"] = new(player, "BASE") } };
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, "moonveil-hidden-target-" + player));
        try
        {
            await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await driver.Act(player, new PassFocusCommand());
            await driver.Act(opponent, new PassFocusCommand());
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["EQUIP"]));
            var pending = Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
            var generation = pending.TargetGenerations!["EQUIP"];
            Assert.Equal(player, driver.State.PriorityPlayerId);
            await driver.Act(player, new TapRuneCommand("OWN-RUNE"));
            var cast = await driver.Act(player, new PlayCardCommand("PULL", "SFD·135/221", ["EQUIP"]));
            Assert.Equal(1, Assert.Single(cast.Events, e => e.Kind == "COST_PAID").Payload["totalManaCost"]);
            await driver.Top();
            Assert.Contains("EQUIP", driver.State.PlayerZones[player].Hand);
            Assert.True(driver.State.CardObjects["EQUIP"].ObjectGeneration > generation);
            var retained = Assert.Single(driver.State.StackItems, i => i.StackItemId == pending.StackItemId);
            Assert.Equal(generation, retained.TargetGenerations!["EQUIP"]);
            var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(driver.State)[opponent],
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var row = Assert.Single(view.GetProperty("stack").EnumerateArray(),
                e => e.GetProperty("stackItemId").GetString() == pending.StackItemId);
            Assert.Equal("HIDDEN", Assert.Single(row.GetProperty("targetObjectIds").EnumerateArray()).GetString());
            var resolved = await driver.Top();
            Assert.DoesNotContain(resolved.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
            Assert.True(driver.State.CardObjects["BACKUP"].IsExhausted);
            Assert.Null(driver.State.PendingCardChoice);
            await RideTheWindCurrentMovementTests.Replay(initial, driver);
        }
        finally { driver.Export("summary.json", new { initial, state = driver.State, commands = driver.Commands(), events = driver.Events() }); }
    }
}
