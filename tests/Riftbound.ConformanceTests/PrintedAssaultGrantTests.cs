using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedAssaultGrantTests
{
    private static MatchState Position(string card, bool repeat = false)
    {
        var state = VenUnitEntryBasicTests.Position(card);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = Printed("ALLY", "OGN·088/298", "P1"),
            ["ENEMY"] = Printed("ENEMY", "OGN·176/298", "P2"),
            ["EXTRA"] = Printed("EXTRA", "OGN·176/298", "P2") },
            RunePools = state.RunePools.ToDictionary(p => p.Key, _ => new RunePool(40, 10,
                new Dictionary<string, int> { ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 })) };
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = LocalDestructionRecallTests.At(state, "EXTRA", "P2", "BATTLEFIELD", "OTHER");
        if (repeat && card == "UNL-017/219") state = MightyExperienceFrameTests.HandUnit(state, "FODDER", "UNL-092/219");
        else if (repeat && card != "SFD·003/221") state = state with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        return state;
    }

    private static int Might(Driver game) => ThreeBattlefieldTimingProbe.EffectivePower(game.State, "P1", "ALLY");
    private static PlayCardCommand Spell(string card, bool repeat = false)
        => new("UNIT", card, ["ALLY"], OptionalCosts: repeat
            ? card == "UNL-017/219" ? ["DISCARD_HAND_CARD:FODDER"] : ["ECHO"] : []);

    public static IEnumerable<object[]> Cases() => new[] { ("OGN·004/298", 3), ("SFD·003/221", 2),
        ("UNL-010/219", 2), ("UNL-017/219", 4) }.SelectMany(row => new[] { false, true }.Select(repeat => new object[] { row.Item1, row.Item2, repeat }));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AGrantCastInBaseAppliesOnALaterRealAttackAndEndsWithTheAttackRole(string card, int amount, bool repeat)
    {
        var game = new Driver(Position(card, repeat));
        await game.Act("P1", Spell(card, repeat)); await game.Top();
        Assert.Equal(8, Might(game)); Assert.Equal(8, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(repeat ? 2 : 1, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(effect => effect.StartsWith("COMBAT_KEYWORD:强攻" + amount + ":", StringComparison.Ordinal)));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        await FinishBattle(game, 8 + amount * (repeat ? 2 : 1));
        Assert.False(game.State.CardObjects["ALLY"].IsAttacking); Assert.Equal(8, Might(game));
        Assert.Equal(8, game.State.CardObjects["ALLY"].Power);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, effect => effect.StartsWith("COMBAT_KEYWORD:强攻", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·004/298", 3)] [InlineData("SFD·003/221", 2)] [InlineData("UNL-010/219", 2)]
    public async Task ARealSwiftPlayDuringTheDuelDoesNotLeaveAnUnconditionalPowerBonusAfterBattle(string card, int amount)
    {
        var game = new Driver(Position(card));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        Assert.True(game.State.CardObjects["ALLY"].IsAttacking);
        await game.Act("P1", Spell(card)); await game.Top();
        Assert.Equal(8 + amount, Might(game)); Assert.Equal(8, game.State.CardObjects["ALLY"].Power);
        await FinishBattle(game, 8 + amount); Assert.Equal(8, Might(game)); await game.Replay();
    }

    [Fact]
    public async Task ARecalledTargetDoesNotGiveItsNewPrivateIncarnationTheOldAssault()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("OGN·004/298"), "RECALL", "OGN·104/298", "P1");
        var generation = initial.CardObjects["ALLY"].ObjectGeneration;
        var game = new Driver(initial); await game.Act("P1", Spell("OGN·004/298"));
        if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RECALL", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, effect => effect.StartsWith("COMBAT_KEYWORD:强攻", StringComparison.Ordinal));
        Assert.Equal(8, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task PowerPunchStillGrantsRoamAlongsideTheIndependentAssault()
    {
        var initial = VenUnitEntryBasicTests.Position("UNL-010/219");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = Printed("ALLY", "OGN·088/298", "P1") } };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P1", Spell("UNL-010/219")); await game.Top();
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    private static async Task FinishBattle(Driver game, int expected)
    {
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(game.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY", "EXTRA"], OptionalCosts: ["COMBAT_ASSIGNMENT"]));
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, candidate => candidate.Action == CommandTypes.AssignCombatDamage);
        var pool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["damagePool"]);
        Assert.Equal(expected, pool["ALLY"]); Assert.Equal(expected, Might(game));
        await game.Act("P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER", [new("ALLY", "ENEMY", 2), new("ALLY", "EXTRA", expected - 2)]));
        await game.Act("P2", new AssignCombatDamageCommand("battle:OTHER", "OTHER", [new("ENEMY", "ALLY", 2), new("EXTRA", "ALLY", 2)]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Battlefields);
    }
}
