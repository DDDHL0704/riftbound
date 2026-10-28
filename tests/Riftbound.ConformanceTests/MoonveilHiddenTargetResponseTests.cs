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
    public async Task SyntheticReturnToHandHidesTheConfirmedTargetAndDoesNotRetarget(string player)
    {
        // This controlled component transition is not an actual response: SFD·135/221
        // has Swift, not Reaction, and cannot be played in this closed window.
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BACKUP"] = new("BACKUP", cardNo: "SFD·019/221", isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard], ownerId: player, controllerId: player) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Base = initial.PlayerZones[player].Base.Append("BACKUP").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["BACKUP"] = new(player, "BASE") } };
        var before = new ThreeBattlefieldTimingProbe.Trace(initial, "moonveil-hidden-before-" + player);
        try
        {
            await before.Conquer(player);
            await before.Choose(true, "EQUIP", targetMustBeChosen: true);
            var pending = Assert.Single(before.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
            var generation = pending.TargetGenerations!["EQUIP"];
            var zones = new Dictionary<string, PlayerZones>(before.State.PlayerZones) {
                [player] = before.State.PlayerZones[player] with {
                    Base = before.State.PlayerZones[player].Base.Where(id => id != "EQUIP").ToArray(),
                    Hand = before.State.PlayerZones[player].Hand.Append("EQUIP").ToArray() } };
            var cards = new Dictionary<string, CardObjectState>(before.State.CardObjects) {
                ["EQUIP"] = before.State.CardObjects["EQUIP"] with { ObjectGeneration = generation + 1, IsExhausted = false } };
            var locations = new Dictionary<string, ObjectLocationState>(before.State.ObjectLocations) {
                ["EQUIP"] = new(player, "HAND") };
            var changed = before.State with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
            var retained = Assert.Single(changed.StackItems, i => i.StackItemId == pending.StackItemId);
            Assert.Equal(generation, retained.TargetGenerations!["EQUIP"]);
            var opponent = ThreeBattlefieldTimingProbe.Other(player);
            var snapshots = ResolutionResult.BuildSnapshots(changed);
            var prompts = ResolutionResult.BuildPrompts(changed);
            var views = changed.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
                changed.Tick, 0, snapshots[id], changed.Tick, 0, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(changed.RoomId, changed.Tick, 0, [], changed);
            var errors = MatchRecoveryValidator.ValidateComponentStateAndViews(changed, views, spectator);
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            foreach (var snapshot in new[] { snapshots[opponent], spectator.SpectatorSnapshot })
            {
                var view = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var row = Assert.Single(view.GetProperty("stack").EnumerateArray(),
                    e => e.GetProperty("stackItemId").GetString() == pending.StackItemId);
                Assert.Equal("HIDDEN", Assert.Single(row.GetProperty("targetObjectIds").EnumerateArray()).GetString());
            }
            var after = new ThreeBattlefieldTimingProbe.Trace(changed, "moonveil-hidden-after-" + player);
            try
            {
                var resolved = await after.Top();
                Assert.DoesNotContain(resolved.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
                Assert.True(after.State.CardObjects["BACKUP"].IsExhausted);
                Assert.Null(after.State.PendingCardChoice);
            }
            finally { after.Save(); }
        }
        finally { before.Save(); }
    }
}
