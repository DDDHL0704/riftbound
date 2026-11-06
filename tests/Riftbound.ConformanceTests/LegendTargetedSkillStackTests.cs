using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LegendTargetedSkillStackTests
{
    private const string Lee = LegendActionAbilityCatalog.LeeSinLegendAbilityId;
    private const string Fortune = LegendActionAbilityCatalog.MissFortuneLegendAbilityId;
    private const string Khazix = LegendActionAbilityCatalog.KhazixLegendBoonAbilityId;

    public static IEnumerable<object[]> Printings() => new[] { Lee, Fortune, Khazix }
        .SelectMany(ability => LegendActionAbilityCatalog.SourceCardNosForAbility(ability)
            .Select(card => new object[] { card, ability }));

    private static string[] Costs(string ability)
        => ability == Lee ? ["SPEND_MANA:1"] : ability == Khazix ? ["SPEND_EXPERIENCE:1"] : [];
    private static LegendActCommand Command(string ability, string target = "HOST")
        => new("LEGEND", ability, [target], Costs(ability));
    private static MatchState Position(string card)
    {
        var state = LegendAttachmentTests.PositionFor(card);
        state = state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 3 },
            RunePools = state.RunePools.ToDictionary(pair => pair.Key, _ => new RunePool(20, 10)),
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·219/298", "P2") } };
        return LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    }
    private static bool HasEffect(MatchState state, string ability, string target = "HOST")
        => ability == Fortune ? state.CardObjects[target].UntilEndOfTurnEffects.Contains("ROAM")
            : state.CardObjects[target].BoonCount > 0;

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryPrintingPaysAndExhaustsBeforeTheSkillResolvesThroughTwoResponsePasses(string card, string ability)
    {
        var game = new Driver(Position(card));
        await game.Act("P1", Command(ability));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(HasEffect(game.State, ability));
        Assert.Equal(ability == Lee ? 19 : 20, game.State.RunePools["P1"].Mana);
        Assert.Equal(ability == Khazix ? 2 : 3, game.State.PlayerExperience["P1"]);
        var item = Assert.Single(game.State.StackItems);
        Assert.NotNull(item.LegendUnitEffect); Assert.Equal(card, item.CardNo);
        Assert.Equal(game.State.CardObjects["HOST"].ObjectGeneration, item.TargetGenerations!["HOST"]);
        await game.Top();
        Assert.True(HasEffect(game.State, ability)); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·257/298", Lee)]
    [InlineData("OGN·267/298", Fortune)]
    [InlineData("UNL-201/219", Khazix)]
    public async Task AResponseReturningTheTargetToHandPreventsTheEffectOnItsNewGeneration(string card, string ability)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); await game.Act("P1", Command(ability));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Top();
        Assert.False(HasEffect(game.State, ability));
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(ability == Khazix ? 2 : 3, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·267/298", Fortune)]
    [InlineData("UNL-201/219", Khazix)]
    public async Task AnEnemyUnitIsLegalAndItsCurrentWardIsPaidBeforeTheSkillIsQueued(string card, string ability)
    {
        var initial = Position(card);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] } } };
        var game = new Driver(initial); await game.Act("P1", Command(ability, "ENEMY"));
        Assert.False(HasEffect(game.State, ability, "ENEMY"));
        Assert.Equal(8, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(game.State.StackItems).LegendUnitEffect!.SpellshieldTaxPower);
        await game.Top(); Assert.True(HasEffect(game.State, ability, "ENEMY")); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·257/298", Lee, "enemy")]
    [InlineData("UNL-201/219", Khazix, "experience")]
    [InlineData("UNL-201/219", Khazix, "ward")]
    [InlineData("OGN·267/298", Fortune, "ward")]
    public async Task IllegalScopeOrUnpaidCostsRejectWithoutExhaustingOrChangingState(string card, string ability, string branch)
    {
        var state = Position(card); var target = branch == "experience" ? "HOST" : "ENEMY";
        if (branch == "experience") state = state with { PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 0 } };
        if (branch == "ward") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } };
        var before = MatchStateHasher.Hash(state); var command = Command(ability, target);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-legend-target", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task ATargetedLegendSkillCanBeCounteredDuringTheNormalResponseWindow()
    {
        var state = AttackDamageDistributionTests.Spell(Position("OGN·267/298"), "COUNTER", "UNL-106/219", "P2");
        var game = new Driver(state); await game.Act("P1", Command(Fortune, "ENEMY"));
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "UNL-106/219", ["ENEMY", skill.StackItemId]));
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.False(HasEffect(game.State, Fortune, "ENEMY"));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedSourceTargetCostAndAbilityBindings()
    {
        var game = new Driver(Position("OGN·257/298")); await game.Act("P1", Command(Lee));
        var item = Assert.Single(game.State.StackItems); var context = item.LegendUnitEffect!;
        foreach (var corrupt in new[] {
            item with { LegendUnitEffect = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { LegendUnitEffect = context with { SpellshieldTaxPower = 1 } },
            item with { LegendUnitEffect = context with { AbilityId = Khazix } },
            item with { OptionalCosts = ["SPEND_MANA:0"] },
            item with { TargetGenerations = new Dictionary<string, long> { ["HOST"] = game.State.CardObjects["HOST"].ObjectGeneration + 1 } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [corrupt] }));
        await game.Top(); await game.Replay();
    }
}
