using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulLastBreathTests
{
    internal static LastBreathMightContext BelowThresholdContext(string source, string controller)
    {
        var unit = new CardObjectState(source, power: 4, cardNo: "SFD·167/221", ownerId: controller,
            controllerId: controller, tags: [CardObjectTags.UnitCard]);
        var zones = new Dictionary<string, PlayerZones> { [controller] = PlayerZones.Empty with { Base = [source] } };
        return new(source, "SFD·167/221", 0, controller, 4, new(zones,
            new Dictionary<string, CardObjectState> { [source] = unit },
            new Dictionary<string, ObjectLocationState> { [source] = new(controller, "BASE") }, [],
            new Dictionary<string, int> { [controller] = 0 }), $"PRINTED:{source}:0");
    }

    internal static async Task<ResolutionResult> Destroy(MatchState initial, string target = "HOST")
    {
        initial = PowerfulConsumerMightTests.Hand(initial, "OGN·229/298");
        return await Top((await Act(initial, "P1", new PlayCardCommand("SPELL", "OGN·229/298", [target]))).State);
    }
    [Fact]
    public async Task ExplicitDestructionCapturesThePowerfulUnitsCurrentMightBeforeItLeaves()
    {
        // Current official Unsung Hero: printed 2, equipped with printed +3.
        var initial = ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true);
        Assert.Equal(5, ArmamentMightTests.Might(initial)); Assert.Equal(2, initial.CardObjects["HOST"].Power);
        initial = PowerfulConsumerMightTests.Hand(initial, "OGN·229/298");
        var destroyed = await Top((await Act(initial, "P1", new PlayCardCommand("SPELL", "OGN·229/298", ["HOST"]))).State);
        Assert.Contains("HOST", destroyed.State.PlayerZones["P1"].Graveyard);
        var trigger = Assert.Single(destroyed.State.StackItems);
        Assert.Equal(TriggerKinds.UnitLastBreathPowerfulDraw, trigger.EffectKind);
        Assert.Equal(5, trigger.LastBreathMight!.Might);
        Assert.Empty(destroyed.State.PlayerZones["P1"].Hand);
        Restore(destroyed.State); ArmamentMightTests.PublicRecovery(destroyed.State);
        var resolved = await Top(destroyed.State);
        Assert.Equal(2, resolved.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(resolved.State.StackItems);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task ExplicitDestructionQueuesTheLastBreathForIndependentConfirmationAndResponse()
    {
        // The existing authoritative fixture scalar is 5; this case isolates timing.
        var initial = ArmamentMightTests.Position(unit: "SFD·167/221", power: 5);
        initial = PowerfulConsumerMightTests.Hand(initial, "OGN·229/298");
        var destroyed = await Top((await Act(initial, "P1", new PlayCardCommand("SPELL", "OGN·229/298", ["HOST"]))).State);
        Assert.Contains("HOST", destroyed.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(destroyed.State.PlayerZones["P1"].Hand);
        var queued = Assert.Single(destroyed.State.StackItems);
        Assert.Equal(TriggerKinds.UnitLastBreathPowerfulDraw, queued.EffectKind);
        Assert.DoesNotContain(destroyed.Events, e => e.Kind == "TRIGGER_RESOLVED" &&
            Equals(e.Payload.GetValueOrDefault("effectKind"), TriggerKinds.UnitLastBreathPowerfulDraw));
        Restore(destroyed.State); ArmamentMightTests.PublicRecovery(destroyed.State);
        var resolved = await Top(destroyed.State);
        Assert.Equal(2, resolved.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(resolved.State.StackItems);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task APrintedCopyProvidesASeparateLastBreathThroughTheSharedOrderingFlow()
    {
        var state = ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true);
        state = AttachedRuleTextTests.AddSong(state, "SONG");
        state = (await AttachedRuleTextTests.Attach(state, "SONG")).State;
        var destroyed = await Destroy(state);
        Assert.Equal(2, destroyed.State.TriggerQueue.Count); Assert.Empty(destroyed.State.StackItems);
        Assert.Single(destroyed.State.TriggerQueue, t => t.LastBreathMight!.Origin is null);
        Assert.Single(destroyed.State.TriggerQueue, t => t.LastBreathMight!.Origin is not null);
        Assert.Equal(2, destroyed.State.TriggerQueue.Select(t => t.TriggerId).Distinct().Count());
        Assert.Empty(destroyed.State.PlayerZones["P1"].Hand);
        var errors = OfficialInsightAndSpellLockTests.Errors(destroyed.State); Assert.True(errors.Count == 0, string.Join("; ", errors)); Restore(destroyed.State);
        var ordered = await Act(destroyed.State, "P1", new OrderTriggersCommand(
            OrderedTriggerIds: destroyed.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Restore(ordered.State); ArmamentMightTests.PublicRecovery(ordered.State);
        var first = await Top(ordered.State); Assert.Equal(2, first.State.PlayerZones["P1"].Hand.Count);
        var second = await Top(first.State); Assert.Equal(4, second.State.PlayerZones["P1"].Hand.Count);
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
    }

    [Fact]
    public async Task AStoredPowerfulUnitSuppressedByTheLiveAuraDoesNotEarnLastBreath()
    {
        var state = PowerfulConsumerMightTests.Suppressed();
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = "SFD·167/221" } } };
        state = (await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []))).State;
        Assert.Equal(2, ArmamentMightTests.Might(state)); Assert.Equal(8, state.CardObjects["HOST"].Power);
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
        state = PowerfulConsumerMightTests.Hand(state, "SFD·163/221");
        var destroyed = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "SFD·163/221", ["HOST", "ALLY"]))).State);
        Assert.Single(destroyed.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(destroyed.State.StackItems, i => i.EffectKind == TriggerKinds.UnitLastBreathPowerfulDraw);
        Assert.DoesNotContain(destroyed.State.TriggerQueue, t => t.EffectKind == TriggerKinds.UnitLastBreathPowerfulDraw);
        Restore(destroyed.State); ArmamentMightTests.PublicRecovery(destroyed.State);
    }

    [Fact]
    public async Task ADepartedCopyTokenStillResolvesItsEarnedLastBreath()
    {
        var state = (await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("SFD·167/221"))).State;
        const string token = "C-TOKEN-001";
        state = LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = new("GEAR", cardNo: "SFD·056/221", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: token) } }, "GEAR", "P1", "BASE");
        var destroyed = await Destroy(state, token);
        Assert.False(destroyed.State.CardObjects.ContainsKey(token));
        Assert.Equal(5, Assert.Single(destroyed.State.StackItems).LastBreathMight!.Might);
        Restore(destroyed.State); ArmamentMightTests.PublicRecovery(destroyed.State);
        var resolved = await Top(destroyed.State); Assert.Equal(2, resolved.State.PlayerZones["P1"].Hand.Count);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("might")] [InlineData("generation")] [InlineData("instance")]
    [InlineData("private-pile")] [InlineData("key-drift")]
    public async Task RecoveryRejectsForgedLastKnownMightInputs(string change)
    {
        var state = (await Destroy(ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true))).State;
        var item = Assert.Single(state.StackItems); var captured = item.LastBreathMight!;
        captured = change switch {
            "might" => captured with { Might = 99 },
            "generation" => captured with { SourceGeneration = 9 },
            "instance" => captured with { InstanceId = "FORGED" },
            "private-pile" => captured with { Frame = captured.Frame with {
                PlayerZones = new Dictionary<string, PlayerZones>(captured.Frame.PlayerZones) {
                    ["P2"] = captured.Frame.PlayerZones["P2"] with { Hand = ["SECRET"] } } } },
            "key-drift" => captured with { Frame = captured.Frame with {
                CardObjects = new Dictionary<string, CardObjectState>(captured.Frame.CardObjects) {
                    ["HOST"] = captured.Frame.CardObjects["HOST"] with { ObjectId = "FORGED" } } } },
            _ => captured
        };
        var forged = state with { StackItems = [item with { LastBreathMight = captured }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured last-breath Might", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")] [InlineData("spectator")]
    public async Task EveryPublicViewRejectsAChangedHistoricalMight(string viewer)
    {
        var state = (await Destroy(ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true))).State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0,
            snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var target = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(target.Stack));
        var context = Assert.IsType<LastBreathMightContext>(item["lastBreathMight"]);
        item["lastBreathMight"] = context with { Might = 99 };
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
            e => e.Contains("captured last-breath Might", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoricalMightDoesNotStoreOpponentPrivateCardsOrPileOrder()
    {
        var state = ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECRET-HAND"] = new("SECRET-HAND", cardNo: "OGN·229/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]),
            ["SECRET-BANISH"] = new("SECRET-BANISH", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2",
                isFaceDown: true, tags: [CardObjectTags.UnitCard]),
            ["SECRET-DECK"] = new("SECRET-DECK", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Hand = ["SECRET-HAND"], MainDeck = ["SECRET-DECK"], Banished = ["SECRET-BANISH"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["SECRET-BANISH"] = new("P2", "BANISHED"),
                ["SECRET-HAND"] = new("P2", "HAND"), ["SECRET-DECK"] = new("P2", "MAIN_DECK") } };
        var destroyed = await Destroy(state); var context = Assert.Single(destroyed.State.StackItems).LastBreathMight!;
        var serialized = System.Text.Json.JsonSerializer.Serialize(context);
        Assert.DoesNotContain("SECRET-HAND", serialized); Assert.DoesNotContain("SECRET-DECK", serialized); Assert.DoesNotContain("SECRET-BANISH", serialized);
        Assert.All(context.Frame.PlayerZones.Values, zones => {
            Assert.Empty(zones.Hand); Assert.Empty(zones.MainDeck); Assert.Empty(zones.RuneDeck);
        });
        Restore(destroyed.State); ArmamentMightTests.PublicRecovery(destroyed.State);
    }
}
