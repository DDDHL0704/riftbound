using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VilemawCombatDamageTests
{
    private static MatchState Position(string no, string enemy = "SFD·094/221")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "HOST", no, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "DEFENDER", enemy, "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "TRAIN", "OGN·058/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BURN", "OGS·003/024", "P2", "HAND");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(enemy, out var printed));
        // PrintedCardFactory creates identity/type tags. This declared opening
        // also carries the official unit's intrinsic keywords, as normal play does.
        var defender = state.CardObjects["DEFENDER"];
        defender = defender with { Tags = defender.Tags.Concat((printed.SourceUnitTags ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray() };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" }, ["DEFENDER"] = defender } };
    }

    private static async Task OpenAssignments(Driver game)
    {
        for (var step = 0; step < 4 && game.State.FocusPlayerId is { } player; step++)
            await game.Act(player, new PassFocusCommand());
        var battle = game.State.BattleState; Assert.True(battle.IsActive);
        await game.Act("P1", new DeclareBattleCommand("BF", battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
    }

    private static async Task FinishDamage(Driver game)
    {
        for (var step = 0; step < 20 && game.State.BattleState.IsActive && game.State.PendingBattleResult is null; step++)
        {
            var prompt = game.State.Seats.Keys.Select(game.PromptFor).FirstOrDefault(prompt => prompt.Actionable
                && (prompt.Actions.Contains(CommandTypes.AssignCombatDamage) || prompt.Actions.Contains(CommandTypes.PassPriority)));
            if (prompt is null) break;
            var battle = game.State.BattleState;
            GameCommand command = prompt.Actions.Contains(CommandTypes.AssignCombatDamage)
                ? new AssignCombatDamageCommand(battle.BattleId!, battle.BattlefieldObjectId!, CombatTestDriver.Assignments(prompt))
                : new PassPriorityCommand();
            await game.Act(prompt.PlayerId, command);
        }
    }

    private static int CombatDamage(Driver game, string source)
        => game.Events().Where(entry => entry.Event.Kind == "DAMAGE_APPLIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), source)
            && entry.Event.Payload.ContainsKey("combatRole"))
            .Sum(entry => Convert.ToInt32(entry.Event.Payload.GetValueOrDefault("damage")));

    [Theory]
    [InlineData("UNL-060/219")] [InlineData("UNL-060a/219")]
    public async Task BothOfficialWordingsParseTheSameCombatOnlyCurrentMightRestriction(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(8, card.Power);
        Assert.Single(StaticAbilityParser.Parse(card.CardEffect!, []),
            ability => ability.Kind == StaticAbilityKinds.HereLowerMightEnemyUnitCombatDamagePrevention);
    }

    [Theory]
    [InlineData("UNL-060/219", "SFD·094/221", 7, 0)]
    [InlineData("UNL-060a/219", "SFD·094/221", 7, 0)]
    [InlineData("UNL-060/219", "OGN·088/298", 8, 8)]
    [InlineData("UNL-060a/219", "OGN·088/298", 8, 8)]
    [InlineData("UNL-060/219", "VEN·138", 8, 8)]
    [InlineData("UNL-060a/219", "VEN·138", 8, 8)]
    [InlineData("UNL-060/219", "UNL-060/219", 8, 8)]
    [InlineData("UNL-060a/219", "UNL-060a/219", 8, 8)]
    public async Task ARealBattleUsesCurrentMightForDamageSuppressionButKeepsTheOpponentsLethalThreshold(
        string no, string enemy, int enemyMight, int enemyDamage)
    {
        var game = new Driver(Position(no, enemy));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(enemyMight, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["DEFENDER"]));
        var battle = game.State.BattleState;
        Assert.Equal(enemyDamage, ResolutionResult.BattleDamagePoolFor(game.State, battle)["DEFENDER"]);
        Assert.Equal(enemyMight, ResolutionResult.BattleLethalDamageThresholdFor(game.State, battle)["DEFENDER"]);
        await OpenAssignments(game);
        await FinishDamage(game);
        Assert.Equal(enemyDamage, CombatDamage(game, "DEFENDER")); Assert.Equal(8, CombatDamage(game, "HOST"));
        Assert.Contains("DEFENDER", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(enemyDamage == 8, game.State.PlayerZones["P1"].Graveyard.Contains("HOST")); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-060/219")] [InlineData("UNL-060a/219")]
    public async Task ARealResponseBuffChangesTheMightComparisonBeforeDamageAssignments(string no)
    {
        var game = new Driver(Position(no, "OGN·088/298"));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("TRAIN", "OGN·058/298", ["HOST"])); await game.Top();
        Assert.Equal(10, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["HOST"]));
        await OpenAssignments(game); await FinishDamage(game);
        Assert.Equal(0, CombatDamage(game, "DEFENDER")); Assert.Equal(10, CombatDamage(game, "HOST")); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-060/219")] [InlineData("UNL-060a/219")]
    public async Task MovingTheSourceToBaseBeforeCombatRestoresTheEnemyDamageAtTheOriginalBattlefield(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "FIGHTER", "OGN·088/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: ["HOST", "FIGHTER"]));
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await OpenAssignments(game); await FinishDamage(game);
        Assert.Equal(7, CombatDamage(game, "DEFENDER")); Assert.Equal(8, CombatDamage(game, "FIGHTER"));
        Assert.Equal(0, CombatDamage(game, "HOST")); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-060/219")] [InlineData("UNL-060a/219")]
    public async Task CurrentControlRatherThanPhysicalOwnershipDeterminesTheAffectedEnemies(string no)
    {
        var initial = Position(no); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        await OpenAssignments(game); await FinishDamage(game);
        Assert.Equal(0, CombatDamage(game, "DEFENDER")); Assert.Equal(8, CombatDamage(game, "HOST")); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-060/219")] [InlineData("UNL-060a/219")]
    public async Task CombatDamagePreventionDoesNotProtectItsSourceFromOrdinaryEnemySpellDamage(string no)
    {
        var initial = Position(no); initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = RadUnitEffectTestFixture.Place(initial, "DEFENDER", "P2", "BATTLEFIELD", "OTHER");
        initial = RadUnitEffectTestFixture.Place(initial, "HOST", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("BURN", "OGS·003/024", ["HOST"])); await game.Top();
        Assert.Equal(2, game.State.CardObjects["HOST"].Damage);
        Assert.Equal(8, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["HOST"])); await game.Replay();
    }
}
