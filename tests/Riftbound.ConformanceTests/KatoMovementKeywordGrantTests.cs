using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class KatoMovementKeywordGrantTests
{
    private const string Kato = "SFD·112/221", Cloth = "SFD·064/221";
    private static MatchState Position()
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", Kato, "P1", "BASE");
        s = RadGearTestPosition.Put(s, "GEAR", Cloth, "P1", "BASE");
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "ADJUST", "SFD·011/221", "P1", "HAND");
        return RadGearTestPosition.Put(s, "HAND-UNIT", "OGN·001/298", "P1", "HAND");
    }
    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
    private static Task Choose(Driver game, params string[] targets)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId, "TRIGGER_CONFIRMATION", targets));
    private static async Task Close(Driver game)
    {
        for (var n = 0; game.State.SpellDuelState.IsActive; n++)
        {
            Assert.InRange(n, 0, 12);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
    }

    [Fact]
    public async Task OfficialFaqErratumChoosesAnotherUnitAndTheNativeMovementSkill()
    {
        var printing = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Kato);
        Assert.Contains("让另一名友方单位", printing.CardEffect);
        var spec = Assert.Single(TriggerParser.Parse(printing.CardEffect), t => t.Kind == TriggerKinds.UnitMovedGrantSourceKeywordsAndPower);
        Assert.False(spec.Optional); Assert.Equal("BATTLEFIELD", spec.MovementDestinationZone);
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await Move(game); await Choose(game, "ALLY");
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(0, CoreRuleEngine.SpellshieldPowerCostForTarget(game.State, "P2", "ALLY"));
        await game.Top(); Assert.Equal(before + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(1, CoreRuleEngine.SpellshieldPowerCostForTarget(game.State, "P2", "ALLY")); await game.Replay();
    }

    [Theory]
    [InlineData("")] [InlineData("HOST")] [InlineData("ENEMY")] [InlineData("GEAR")] [InlineData("HAND-UNIT")]
    public async Task SharedConfirmationRejectsDeclineSelfEnemiesEquipmentAndHiddenUnits(string target)
    {
        var game = new Driver(Position()); await Move(game);
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId,
            "TRIGGER_CONFIRMATION", target.Length == 0 ? [] : [target]));
        await Choose(game, "ALLY"); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CurrentArmamentMightAndGrantedKeywordsAreLockedOnlyWhenTheSkillResolves(bool response)
    {
        var s = RadGearTestPosition.Put(Position(), "SECOND", "SFD·022/221", "P1", "BASE");
        s = RadGearTestPosition.Put(s, "REMOVE", "SFD·011/221", "P1", "HAND");
        var game = new Driver(s); var before = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_BLUE"])); await game.Top();
        await Move(game); await Choose(game, "ALLY");
        if (response) { await game.Act("P1", new PlayCardCommand("ADJUST", "SFD·011/221", ["HOST", "SECOND"])); await game.Top(); }
        var copiedMight = ArmamentMightTests.Might(game.State); await game.Top();
        Assert.Equal(before + copiedMight, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, SnapshotKeywordGrantRules.Amount(game.State.CardObjects["ALLY"], "坚守"));
        Assert.Equal(1, CoreRuleEngine.SpellshieldPowerCostForTarget(game.State, "P2", "ALLY"));
        await game.Act("P1", new PlayCardCommand("REMOVE", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
        Assert.Equal(before + copiedMight, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, SnapshotKeywordGrantRules.Amount(game.State.CardObjects["ALLY"], "坚守")); await game.Replay();
    }

    [Theory]
    [InlineData("HOST")] [InlineData("ALLY")]
    public async Task ReturningTheReferencedIncarnationOrTargetDoesNotGiveHistoricalCharacteristicsToANewObject(string returned)
    {
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await Move(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top(); await game.Top();
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(0, CoreRuleEngine.SpellshieldPowerCostForTarget(game.State, "P2", "ALLY")); await game.Replay();
    }

    [Fact]
    public async Task SnapshotMightAndKeywordsExpireThroughTheOrdinaryTurnCleanup()
    {
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await Move(game); await Choose(game, "ALLY"); await game.Top(); await Close(game);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, SnapshotKeywordGrantRules.IsGrant);
        Assert.Equal(0, CoreRuleEngine.SpellshieldPowerCostForTarget(game.State, "P2", "ALLY")); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RecoveryRejectsErasingOrDuplicatingAnOriginalKeywordGrant(bool erase)
    {
        var game = new Driver(Position()); await Move(game); await Choose(game, "ALLY"); await game.Top();
        var card = game.State.CardObjects["ALLY"]; var grant = Assert.Single(card.UntilEndOfTurnEffects, SnapshotKeywordGrantRules.IsGrant);
        var changed = card with { UntilEndOfTurnEffects = erase ? card.UntilEndOfTurnEffects.Where(e => e != grant).ToArray()
            : [..card.UntilEndOfTurnEffects, grant] };
        var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) { ["ALLY"] = changed } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Fact]
    public async Task ASharedHuntKeywordActuallyGrantsExperienceOnTheRecipientsOwnConquest()
    {
        var s = RadGearTestPosition.Put(Position(), "MACHETE", "UNL-096/219", "P1", "BASE");
        var game = new Driver(s);
        await game.Act("P1", new AssembleEquipmentCommand("MACHETE", "HOST", ["ASSEMBLE_ORANGE"])); await game.Top();
        await Move(game); await Choose(game, "ALLY"); await game.Top(); await Close(game);
        var before = game.State.PlayerExperience["P1"];
        Assert.Equal(1, SnapshotKeywordGrantRules.Amount(game.State.CardObjects["ALLY"], "狩猎"));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [])); await Close(game);
        Assert.Equal(before + 1, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ASharedDisarmKeywordCreatesItsOwnIndependentAttackSkillAfterTheDonorLeaves()
    {
        var s = RadGearTestPosition.Put(Position(), "PATROLLER", "RAD·055/167", "P1", "BASE");
        var source = s.CardObjects["HOST"];
        // Declared opening: Kato already has the mechanical attribute. The
        // patroller's ordinary current-field aura supplies its Disarm keyword.
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = source with { Tags = [..source.Tags, "机械"], GrantedTags = ["机械"] } } };
        s = CoreRuleEngine.RecomputeRuleDerivedKeywords(s);
        s = RadGearTestPosition.Put(s, "ENEMY", "OGN·034/298", "P2", "BASE");
        s = RadUnitEffectTestFixture.Place(s, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(s); await Move(game); await Choose(game, "ALLY"); await game.Top(); await Close(game);
        Assert.Contains("缴械", game.State.CardObjects["ALLY"].Tags);
        var before = game.State.CardObjects["ENEMY"].Power;
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await Choose(game, "ENEMY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top(); await game.Top();
        Assert.Equal(before - 1, game.State.CardObjects["ENEMY"].Power); await game.Replay();
    }
}
