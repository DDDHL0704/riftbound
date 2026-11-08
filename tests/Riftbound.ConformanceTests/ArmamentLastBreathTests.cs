using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentLastBreathTests
{
    private const string Drive = "SFD·090/221";
    private const string Shears = "SFD·172/221";
    private const string Kill = "OGN·229/298";

    private static MatchState Position(string gear, string owner = "P1")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", gear, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "SFD·135/221", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { OwnerId = owner } } };
    }

    private static async Task Attach(Driver game, string gear)
    {
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY",
            [gear == Drive ? "ASSEMBLE_BLUE" : "ASSEMBLE_YELLOW"]));
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    private static async Task DestroyHost(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["ALLY"]));
        await game.Top();
    }

    [Theory]
    [InlineData(Drive, "P1")] [InlineData(Drive, "P2")]
    [InlineData(Shears, "P1")] [InlineData(Shears, "P2")]
    public async Task ARealAttachmentGrantsTheDestroyedHostsControllerOneIndependentLastBreath(string gear, string owner)
    {
        var game = new Driver(Position(gear, owner));
        await Attach(game, gear);
        await DestroyHost(game);
        Assert.Contains("ALLY", game.State.PlayerZones[owner].Graveyard);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal("ALLY", skill.SourceObjectId);
        Assert.Equal(gear, skill.UnitEvent!.CardNo);
        Assert.True(skill.UnitEvent.Origin!.ArmamentGranted);
        Assert.Equal("GEAR", skill.UnitEvent.Origin.Equipment.ObjectId);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + (gear == Shears ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(gear == Drive, game.State.PlayerZones[owner].Banished.Contains("ALLY"));
        if (gear == Drive)
        {
            var group = Assert.Single(game.State.LinkedExiles);
            Assert.Equal("GEAR", group.SourceObjectId);
            Assert.Equal(owner, group.ArmamentOrigin!.Units["ALLY"].OwnerId);
            Assert.Equal(game.State.CardObjects["ALLY"].ObjectGeneration, group.Objects["ALLY"]);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(Drive)] [InlineData(Shears)]
    public async Task UnattachedEquipmentDoesNotGrantItsLowerBoxToAnotherUnit(string gear)
    {
        var game = new Driver(Position(gear));
        await DestroyHost(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Empty(game.State.LinkedExiles);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(Drive)] [InlineData(Shears)]
    public async Task ReturningTheGrantingEquipmentAfterResolutionPreservesItsPrivateExileAssociation(string gear)
    {
        var game = new Driver(Position(gear)); await Attach(game, gear); await DestroyHost(game);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + (gear == Shears ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(gear == Drive, game.State.PlayerZones["P1"].Banished.Contains("ALLY"));
        var link = MatchStateHasher.HashValue(game.State.LinkedExiles);
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(link, MatchStateHasher.HashValue(game.State.LinkedExiles));
        await game.Replay();
    }

    [Theory]
    [InlineData(Drive)] [InlineData(Shears)]
    public async Task ATokenHostKeepsItsEarnedDrawButCannotBecomeALinkedExiledCard(string gear)
    {
        var state = Position(gear);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1") } };
        var game = new Driver(state); await Attach(game, gear); await DestroyHost(game);
        Assert.DoesNotContain("ALLY", game.State.CardObjects.Keys);
        Assert.Single(game.State.StackItems);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + (gear == Shears ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.LinkedExiles);
        await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("binding")]
    public async Task RecoveryBindsTheEquipmentAndExiledUnitToTheOriginalAcceptedDeathInstruction(string change)
    {
        var game = new Driver(Position(Drive)); await Attach(game, Drive); await DestroyHost(game); await game.Top();
        var group = Assert.Single(game.State.LinkedExiles);
        var forged = game.State with { LinkedExiles = change == "erase" ? []
            : [group with { SourceObjectId = "OTHER-EQUIPMENT" }] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("armament linked exile ledger", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAnInventedFactoryForACeasedArmamentHost()
    {
        var state = Position(Shears);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1") } };
        var game = new Driver(state); await Attach(game, Shears); await DestroyHost(game);
        var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = [skill with {
            UnitEvent = skill.UnitEvent! with { TokenFactoryCardNo = "INVENTED-FACTORY" } }] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("original accepted grant event", StringComparison.Ordinal));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(Drive)] [InlineData(Shears)]
    public async Task ALastBreathAmplifierAddsSeparateGrantedSkillsWithoutExilingTheSameCardTwice(string gear)
    {
        var state = RadUnitEffectTestFixture.Place(Position(gear), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = LastBreathRepetitionTests.AddAmplifier(state, "AMP", "P1", "BF");
        var game = new Driver(state); await Attach(game, gear); await DestroyHost(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top(); await game.Top();
        Assert.Equal(hand + (gear == Shears ? 2 : 0), game.State.PlayerZones["P1"].Hand.Count);
        if (gear == Drive) Assert.Single(game.State.LinkedExiles);
        await game.Replay();
    }
}
