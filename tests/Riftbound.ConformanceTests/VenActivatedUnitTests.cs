using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenActivatedUnitTests
{
    [Theory]
    [InlineData("VEN·060", 3)] [InlineData("VEN·125", 4)]
    public async Task PrintedUnitsPayAndEnterExhaustedAndRejectInventedTargets(string no, int power)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        await Reject(state, new PlayCardCommand("UNIT", no, ["ALLY"]));
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } },
            new PlayCardCommand("UNIT", no, []));
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(16, game.State.RunePools["P1"].Mana); Assert.Equal(power, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task StriderPaysDiscardManaAndExhaustBeforeDamagingEitherSidesBattlefieldUnit(string target)
    {
        var state = InBase("VEN·060");
        state = LocalDestructionRecallTests.At(state, target, target == "ALLY" ? "P1" : "P2", "BATTLEFIELD", target == "ALLY" ? "BF" : "OTHER");
        var game = new Driver(state);
        var command = new ActivateAbilityCommand("UNIT", "SKY_STRIDER_DISCARD_DAMAGE", [target], OptionalCosts: ["DISCARD_HAND_CARD:DISCARD"]);
        await Reject(game.State, command with { OptionalCosts = [] });
        await Reject(game.State, command with { OptionalCosts = ["DISCARD_HAND_CARD:WRONG"] });
        await Reject(game.State, command with { TargetObjectIds = ["GEAR"] });
        await Reject(game.State, command with { TargetObjectIds = ["GRAVE"] });
        await game.Act("P1", command);
        Assert.Contains("DISCARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.CardObjects[target].Damage); await game.Top();
        Assert.Contains(target, game.State.PlayerZones[target == "ALLY" ? "P1" : "P2"].Graveyard);
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), target)
            && Equals(e.Event.Payload.GetValueOrDefault("damage"), 4));
        await Reject(game.State, command); await game.Replay();
    }

    [Fact]
    public async Task StriderRejectsBaseTargetsAndUnpayableFeesWithoutDiscardingOrExhausting()
    {
        var state = InBase("VEN·060");
        var command = new ActivateAbilityCommand("UNIT", "SKY_STRIDER_DISCARD_DAMAGE", ["ENEMY"], OptionalCosts: ["DISCARD_HAND_CARD:DISCARD"]);
        await Reject(state, command);
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } }, command);
        Assert.Contains("DISCARD", state.PlayerZones["P1"].Hand); Assert.False(state.CardObjects["UNIT"].IsExhausted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HunterRequiresAnActualEnemyUnitTargetAndLimitsItsPaidReadyToOncePerTurn(bool initiallyExhausted)
    {
        var initial = AttackDamageDistributionTests.Spell(InBase("VEN·125"), "SPELL", "SFD·080/221", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = initiallyExhausted } } };
        var game = new Driver(initial);
        var command = new ActivateAbilityCommand("UNIT", "TUNDRA_HUNTER_READY", []);
        await Reject(game.State, command);
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"]));
        await game.Top();
        await Reject(game.State, command with { TargetObjectIds = ["ENEMY"] });
        await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } }, command);
        var before = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", command);
        Assert.Equal(before - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(initiallyExhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await Reject(game.State, command);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); await Reject(game.State, command); await game.Replay();
    }

    [Fact]
    public async Task ARejectedEnemyTargetAndAResolvedFriendlyTargetDoNotUnlockHunter()
    {
        var initial = AttackDamageDistributionTests.Spell(InBase("VEN·125"), "SPELL", "SFD·080/221", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        await Reject(initial, new PlayCardCommand("SPELL", "SFD·080/221", ["GRAVE"]));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ALLY"])); await game.Top();
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "TUNDRA_HUNTER_READY", [])); await game.Replay();
    }

    [Fact]
    public async Task HuntersPaidSkillDoesNotReadyOrBuffItsNewHandIncarnation()
    {
        var initial = AttackDamageDistributionTests.Spell(InBase("VEN·125"), "SPELL", "SFD·080/221", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "BOUNCE", "OGN·104/298", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"])); await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "TUNDRA_HUNTER_READY", []));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static MatchState InBase(string no)
    {
        var state = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["DISCARD"] = OfficialGarenConquestBattlefieldCountTests.Printed("DISCARD", "SFD·022/221", "P1"),
                ["WRONG"] = OfficialGarenConquestBattlefieldCountTests.Printed("WRONG", "OGN·219/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = ["DISCARD", "WRONG"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["DISCARD"] = new("P1", "HAND"), ["WRONG"] = new("P1", "HAND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { ["yellow"] = 5, ["red"] = 2, ["blue"] = 2 }) } };
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-activated-unit", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
