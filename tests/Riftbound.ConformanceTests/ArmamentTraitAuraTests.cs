using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed partial class ArmamentTraitAuraTests
{
    private const string MechanicalGear = "SFD·073/221";
    private const string RoamGear = "SFD·192/221";
    private static MatchState Position(string gear, string host = "OGN·219/298")
    {
        var state = VenUnitEntryBasicTests.Position(gear);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = Printed("ALLY", host, "P1"), ["FRIEND"] = Printed("FRIEND", "OGN·176/298", "P1") },
            RunePools = state.RunePools.ToDictionary(p => p.Key, _ => new RunePool(40, 10,
                new Dictionary<string, int> { ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 })) };
        return LocalDestructionRecallTests.At(state, "FRIEND", "P1", "BASE");
    }
    private static bool Has(Driver game, string id, string tag) => game.State.CardObjects[id].Tags.Contains(tag);
    private static async Task Attach(Driver game, string no)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY",
            [no == MechanicalGear ? "ASSEMBLE_BLUE" : "ASSEMBLE_ANY_POWER"])); await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
    }
    private static async Task Detach(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["ALLY", "UNIT"])); await game.Top();
        Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
    }

    [Theory]
    [InlineData(MechanicalGear, "机械")] [InlineData(RoamGear, "游走")]
    public async Task ActualPlayAttachAndDetachGrantOnlyTheLowerBoxHostOrAura(string no, string tag)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no), "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state); Assert.False(Has(game, "ALLY", tag)); await Attach(game, no);
        Assert.True(Has(game, "ALLY", tag)); Assert.False(Has(game, "UNIT", tag));
        Assert.Equal(no == RoamGear, Has(game, "FRIEND", tag)); Assert.False(Has(game, "ENEMY", tag));
        await Detach(game); Assert.False(Has(game, "ALLY", tag)); Assert.False(Has(game, "FRIEND", tag)); await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyPrintedMechanicalTraitSurvivesTheGearLeaving()
    {
        var state = AttackDamageDistributionTests.Spell(Position(MechanicalGear, "OGN·088/298"), "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state); Assert.True(Has(game, "ALLY", "机械")); await Attach(game, MechanicalGear);
        await Detach(game); Assert.True(Has(game, "ALLY", "机械")); await game.Replay();
    }

    [Fact]
    public async Task ARealMechanicalOnlySpellBuffsTheAttachedHostAndLeavesOtherUnitsAlone()
    {
        var state = AttackDamageDistributionTests.Spell(Position(MechanicalGear), "BUFF", "SFD·182/221", "P1");
        var game = new Driver(state); await Attach(game, MechanicalGear);
        var own = game.State.CardObjects["ALLY"].Power; var friend = game.State.CardObjects["FRIEND"].Power;
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·182/221", [])); await game.Top();
        Assert.Equal(own + 1, game.State.CardObjects["ALLY"].Power); Assert.Equal(friend, game.State.CardObjects["FRIEND"].Power);
        Assert.Equal(own + 2, ThreeBattlefieldTimingProbe.EffectivePower(game.State, "P1", "ALLY")); await game.Replay();
    }

    [Fact]
    public async Task TheMechanicalCostReductionUsesTheHostTraitAndEndsOnDetach()
    {
        var state = AttackDamageDistributionTests.Spell(Position(MechanicalGear), "ROBOT", "SFD·076/221", "P1");
        state = AttackDamageDistributionTests.Spell(state, "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state);
        int Quote() {
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, new PlayCardCommand("ROBOT", "SFD·076/221", [])));
            Assert.True(quote.CanPay, quote.Message); return quote.Cost!.Mana;
        }
        var original = Quote(); await Attach(game, MechanicalGear); Assert.Equal(original - 2, Quote());
        await Detach(game); Assert.Equal(original, Quote()); await game.Replay();
    }

    [Fact]
    public async Task MovingTheAuraHostChangesHereAndEnablesTheNewBattlefieldFriendsRoam()
    {
        var state = LocalDestructionRecallTests.At(Position(RoamGear), "FRIEND", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Attach(game, RoamGear);
        Assert.True(Has(game, "ALLY", "游走")); Assert.False(Has(game, "FRIEND", "游走"));
        var move = new MoveUnitCommand("FRIEND", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("no-aura-yet", "P1", move.CmdType), move, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
        Assert.True(Has(game, "ALLY", "游走")); Assert.True(Has(game, "FRIEND", "游走"));
        await game.Act("P1", move); Assert.Equal("OTHER", game.State.ObjectLocations["FRIEND"].BattlefieldObjectId);
        Assert.False(Has(game, "FRIEND", "游走")); Assert.True(Has(game, "ALLY", "游走")); await game.Replay();
    }

    [Fact]
    public async Task RecallingTheAuraHostRemovesTheOldLocationsGrantAndDetachesTheSource()
    {
        var state = AttackDamageDistributionTests.Spell(Position(RoamGear), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Attach(game, RoamGear); Assert.True(Has(game, "FRIEND", "游走"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.False(Has(game, "FRIEND", "游走")); await game.Replay();
    }
}
