using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AkshanIndependentPaidPlayTests
{
    private const string Akshan = "SFD·109/221", Fee = "SPEND_POWER:2:ORANGE";

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", Akshan, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        return RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P2", "BASE");
    }

    [Theory, InlineData("BASE"), InlineData("BATTLEFIELD:BF")]
    public async Task TheOrangeFeeIsPaidBeforeEntryAndTheIndependentTheftChoosesEquipmentAfterEntry(string destination)
    {
        var game = new Driver(Position());
        var resources = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(resources.Mana - 4, game.State.RunePools["P1"].Mana);
        Assert.Equal(resources.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("UNIT", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(game.State.ObjectLocations["UNIT"].BattlefieldObjectId,
            game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task PayingTheAdditionalOrangeDoesNotRequireAnyEnemyEquipmentToExist()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P2", "GRAVEYARD"));
        var resources = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(resources.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningTheOptionalTheftKeepsTheAlreadyPaidCostAndCreatesNoSkill()
    {
        var game = new Driver(Position());
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(power - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheUnpaidUnitEntersWithoutATheftSkillOrAnEquipmentChoice()
    {
        var game = new Driver(Position());
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ReturningTheOriginalSourceBeforeOrAfterResolutionHonorsTheControlDuration(bool resolveFirst)
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        await Confirm(game, "GEAR");
        var skill = Assert.Single(game.State.StackItems).StackItemId;
        if (resolveFirst) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        if (!resolveFirst)
        {
            Assert.Equal(skill, Assert.Single(game.State.StackItems).StackItemId);
            await game.ColdCheckpoint();
            await game.Top();
            // CN 359.3.e.16: it never changes control, even momentarily.
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "EQUIPMENT_CONTROL_CHANGED");
            Assert.Contains(game.Events(), e => e.Event.Kind == "EQUIPMENT_MOVED_TO_BASE");
        }
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("SPEND_POWER:1:ORANGE"), InlineData("SPEND_POWER:2:GREEN")]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:GEAR")]
    public async Task TheFeeIsAnExactAdditionalCostAndCannotEncodeAnEquipmentTarget(string cost)
        => await RadGearTestPosition.Reject(Position(), "P1",
            new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [cost]));

    [Theory, InlineData(false), InlineData(true)]
    public async Task EndingTheLeaseRestoresTheUnderlyingControllerAndKeepsAnotherHostsAttachment(bool ownedByActor)
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "NEXT-UNIT", "SFD·119/221", "P1", "HAND");
        if (ownedByActor) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        await Confirm(game, "GEAR"); await game.Top();
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        await game.Act("P1", new PlayCardCommand("NEXT-UNIT", "SFD·119/221", []));
        var tempered = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", tempered.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(tempered.ChoiceId, tempered.ChoiceWindow, ["GEAR"])); await game.Top();
        Assert.Equal("NEXT-UNIT", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        var gear = game.State.CardObjects["GEAR"];
        Assert.Equal("P2", gear.ControllerId); Assert.Equal(ownedByActor ? "P1" : "P2", gear.OwnerId);
        Assert.Equal("NEXT-UNIT", gear.AttachedToObjectId);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(gear.EquipmentControl);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("underlying"), InlineData("source"), InlineData("erase"), InlineData("null-grant")]
    public async Task RecoveryBindsTheLeaseToItsOriginalAcceptedEffect(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        await Confirm(game, "GEAR"); await game.Top();
        var gear = game.State.CardObjects["GEAR"];
        var lease = Assert.IsType<EquipmentControlLease>(gear.EquipmentControl);
        var altered = mutation switch {
            "underlying" => lease with { UnderlyingControllerId = "P1" },
            "source" => lease with { Grants = [lease.Grants[0] with {
                Source = new("ALLY", game.State.CardObjects["ALLY"].ObjectGeneration) }] },
            "null-grant" => lease with { Grants = [null!] }, _ => null };
        var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["GEAR"] = gear with { EquipmentControl = altered } } };
        var history = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Empty(Validate(game, game.State)); Assert.NotEmpty(Validate(game, forged));
        Assert.Equal(history, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SuccessiveTheftsKeepEachOriginalDurationWhenEitherSourceLeavesFirst(bool underlyingLeavesFirst)
    {
        var initial = RadGearTestPosition.Put(Position(), "SECOND", Akshan, "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN-ONE", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN-TWO", "OGN·104/298", "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "P1-R", "UNL-R04", "P1", "BASE");
        for (var index = 0; index < 8; index++)
            initial = RadGearTestPosition.Put(initial, "P2-R" + index, "UNL-R04", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        await Confirm(game, "GEAR"); await game.Top();
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", Enumerable.Range(0, 8).Select(index => "P2-R" + index));
        await game.Act("P2", new PlayCardCommand("SECOND", Akshan, [], OptionalCosts:
            [Fee, "RECYCLE_RUNE:P2-R0", "RECYCLE_RUNE:P2-R1"]));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["GEAR"].EquipmentControl!.Grants.Count);
        Assert.Equal("SECOND", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Act("P2", new PlayCardCommand("RETURN-TWO", "OGN·104/298", ["SECOND"]));
        if (underlyingLeavesFirst)
        {
            await game.Act("P2", new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("P1-R"));
            await game.Act("P1", new PlayCardCommand("RETURN-ONE", "OGN·104/298", ["UNIT"]));
            await game.Top();
            Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
            Assert.Single(game.State.CardObjects["GEAR"].EquipmentControl!.Grants);
            Assert.Equal("SECOND", game.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        await game.Top();
        if (!underlyingLeavesFirst)
        {
            Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
            await game.Act("P2", new EndTurnCommand());
            // Turn resources expire; use an actual rune for the last response.
            await game.Act("P1", new TapRuneCommand("P1-R"));
            await game.Act("P1", new PlayCardCommand("RETURN-ONE", "OGN·104/298", ["UNIT"])); await game.Top();
        }
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(game.State.CardObjects["GEAR"].EquipmentControl);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base);
        await game.ColdCheckpoint();
    }

    private static IReadOnlyList<string> Validate(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }

    private static async Task Confirm(Driver game, string target)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }
}
