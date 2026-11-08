using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EnemyCountAssaultTests
{
    private const string Berserker = "SFD·131/221";

    private static MatchState Position(int enemies = 2)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", Berserker, "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", enemies > 0 ? "BF" : "OTHER");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·219/298", "P2", "BATTLEFIELD", enemies > 1 ? "BF" : "OTHER");
        state = RadGearTestPosition.Put(state, "RETURN1", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN2", "OGN·104/298", "P2", "HAND");
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    private static int Assault(MatchState state)
        => CardCombatKeywordRules.KeywordAmount(state.CardObjects["HOST"].Tags, CardCombatKeywordNames.Assault);

    private static Task Attack(Driver game)
        => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));

    private static async Task GiveAction(Driver game, string player)
    {
        if (game.State.PriorityPlayerId is { } priority && priority != player)
            await game.Act(priority, new PassPriorityCommand());
        else if (game.State.FocusPlayerId is { } focus && focus != player)
            await game.Act(focus, new PassFocusCommand());
    }

    [Fact]
    public async Task TheOfficialTextParsesALiveCountRatherThanAnEntryOrCombatSnapshot()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == Berserker);
        var parsed = StaticAbilityParser.Parse(card.CardEffect!, []);
        var ability = Assert.Single(parsed, spec => spec.Kind == StaticAbilityKinds.UnitEnemyCountHereSelfKeywords);
        Assert.Equal([CardCombatKeywordNames.Assault], ability.GrantedKeywords);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Berserker, out var behavior));
        Assert.Contains("急速", behavior.SourceUnitTags!); Assert.Equal(4, behavior.SourceUnitPower);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task CurrentEnemiesAtTheActualLocationSupplyAssaultOnlyWhileAttacking(int enemies)
    {
        var game = new Driver(Position(enemies)); Assert.Equal(0, Assault(game.State));
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await Attack(game); Assert.Equal(enemies, Assault(game.State));
        Assert.Equal(4 + enemies, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task ActualEnemyDepartureContinuouslyReducesTheKeywordAndEndsTheCombatBonus()
    {
        var game = new Driver(Position()); await Attack(game);
        Assert.Equal(2, Assault(game.State)); Assert.Equal(6, ArmamentMightTests.Might(game.State));
        foreach (var (spell, target, remaining) in new[] { ("RETURN1", "ENEMY", 1), ("RETURN2", "SECOND", 0) }) {
            await GiveAction(game, "P2");
            await game.Act("P2", new PlayCardCommand(spell, "OGN·104/298", [target])); await game.Top();
            Assert.Equal(remaining, Assault(game.State));
            Assert.Equal(4 + remaining, ArmamentMightTests.Might(game.State));
        }
        Assert.False(game.State.CardObjects["HOST"].IsAttacking); await game.Replay();
    }

    [Fact]
    public async Task FriendshipUsesCurrentControlAndPhysicalOwnershipDoesNotAddAnEnemy()
    {
        var initial = Position() with { CardObjects = new Dictionary<string, CardObjectState>(Position().CardObjects) {
            ["SECOND"] = Position().CardObjects["SECOND"] with { ControllerId = "P1" } } };
        var game = new Driver(CoreRuleEngine.RecomputeRuleDerivedKeywords(initial)); await Attack(game);
        Assert.Equal(1, Assault(game.State)); Assert.Equal(5, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AnIndependentAssaultGrantAddsToTheLiveEnemyCount()
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with {
                Tags = [..initial.CardObjects["HOST"].Tags, "强攻3"], GrantedTags = ["强攻3"] } } };
        var game = new Driver(CoreRuleEngine.RecomputeRuleDerivedKeywords(initial)); await Attack(game);
        Assert.Equal(5, CardCombatKeywordRules.KeywordAmount(game.State.CardObjects["HOST"].RuleDerivedTags ?? [], CardCombatKeywordNames.Assault)
            + CardCombatKeywordRules.KeywordAmount(game.State.CardObjects["HOST"].GrantedTags ?? [], CardCombatKeywordNames.Assault));
        Assert.Equal(9, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AnActuallyCopiedPrintedAbilityContributesItsOwnLiveValueAndDetachingRemovesOnlyThatInstance()
    {
        var initial = RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "DETACH", "SFD·011/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.NotNull(game.State.CardObjects["SONG"].AttachedRuleText);
        var baseline = ArmamentMightTests.Might(game.State); await Attack(game);
        Assert.Equal(4, Assault(game.State)); Assert.Equal(baseline + 4, ArmamentMightTests.Might(game.State));
        await GiveAction(game, "P1");
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "SONG"])); await game.Top();
        Assert.Equal(2, Assault(game.State)); Assert.Equal(6, ArmamentMightTests.Might(game.State)); await game.Replay();
    }
}
