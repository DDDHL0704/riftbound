using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AlphaStrikeReflexiveExperienceTests
{
    internal const string Alpha = "UNL-192/219";
    internal static MatchState Position(int power = 3, int enemies = 2)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "SPELL", Alpha, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SOURCE", "OGN·219/298", "P1", "BASE");
        for (var i = 0; i < enemies; i++) {
            var id = "E" + i; state = RadGearTestPosition.Put(state, id, "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { Power = 1 } } };
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SOURCE"] = state.CardObjects["SOURCE"] with { Power = power } } };
    }

    internal static async Task Allocate(Driver game, int amount = 1)
    {
        while (game.State.PendingRuleChoice is { Request: { Kind: "DISTRIBUTED_DAMAGE_AMOUNT" } choice })
        {
            var option = Assert.Single(choice.Options, option => {
                var parts = option.Id.Split(':'); return int.Parse(parts[^2]) <= amount && amount <= int.Parse(parts[^1]); });
            await game.Act(choice.PlayerId, new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [option.Id]));
        }
    }
    internal static async Task Order(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0) await game.Act(game.State.ActivePlayerId,
            new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    private static int Experience(Driver game) => game.State.PlayerExperience.GetValueOrDefault("P1");

    [Fact]
    public async Task MultipleEnemiesTakeDistributedUnitDamageAndEachActualDeathEarnsOneIndependentExperienceSkill()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"]));
        Assert.Equal(0, game.State.CardObjects["E0"].Damage); await game.Top();
        Assert.Equal("DISTRIBUTED_DAMAGE_AMOUNT", game.State.PendingRuleChoice!.Request.Kind);
        await Allocate(game); Assert.Contains("E0", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("E1", game.State.PlayerZones["P2"].Graveyard); Assert.Equal(0, Experience(game));
        await Order(game); Assert.Equal(2, game.State.StackItems.Count);
        Assert.All(game.State.StackItems, skill => Assert.Equal("SPELL_INSTRUCTION_REFLEXIVE", skill.UnitEvent!.Timing));
        await game.Top(); Assert.Equal(1, Experience(game)); await game.Top(); Assert.Equal(2, Experience(game)); await game.Replay();
    }

    [Fact]
    public async Task InitialEnemyCountCannotExceedTheChosenUnitsMight()
    {
        var game = new Driver(Position(2, 3));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1", "E2"]));
        await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"]));
        await game.Top(); await Allocate(game); await Order(game); while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(2, Experience(game)); Assert.Contains("E2", game.State.PlayerZones["P2"].Battlefields); await game.Replay();
    }

    [Fact]
    public async Task ARealMightReductionRequiresKeepingExactlyTheMaximumLegalNumberOfOriginalTargets()
    {
        var state = RadGearTestPosition.Put(Position(5, 5), "FREEZE", "SFD·066/221", "P2", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1", "E2", "E3", "E4"]));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("FREEZE", "SFD·066/221", ["SOURCE"])); await game.Top(); await game.Top();
        for (var i = 0; i < 2; i++)
        {
            var request = game.State.PendingRuleChoice!.Request; Assert.Equal("DISTRIBUTED_DAMAGE_TARGETS", request.Kind);
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["E" + i]));
        }
        Assert.Null(game.State.PendingRuleChoice); Assert.Contains("E0", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("E1", game.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(new[] { "E2", "E3", "E4", "FREEZE" }, game.State.PlayerZones["P2"].Graveyard.Order(StringComparer.Ordinal));
        await Order(game); while (game.State.StackItems.Count > 0) await game.Top(); Assert.Equal(3, Experience(game)); await game.Replay();
    }

    [Fact]
    public async Task SpellDamagePreventionDoesNotPreventDamageExplicitlyDealtByTheChosenUnit()
    {
        var game = new Driver(Position() with { UntilEndOfTurnEffects = ["PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] });
        await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"])); await game.Top(); await Allocate(game);
        await Order(game); while (game.State.StackItems.Count > 0) await game.Top(); Assert.Equal(2, Experience(game)); await game.Replay();
    }

    [Fact]
    public async Task ASourceReturnBeforeResolutionIgnoresItsAssociatedDamageAndEarnsNoExperience()
    {
        var state = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"]));
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"])); await game.Top(); await game.Top();
        Assert.Equal(0, Experience(game)); Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("E0", game.State.PlayerZones["P2"].Battlefields); await game.Replay();
    }

    [Fact]
    public async Task ARecallReplacementDoesNotCountAsAnActualDestructionForExperience()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["E0"] = state.CardObjects["E0"] with { UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"]));
        await game.Top(); await Allocate(game); await Order(game); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(1, Experience(game)); Assert.Contains("E0", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Fact]
    public async Task EchoCountsActualDeathsAfterTheRepeatedDamageInsteadOfRewardingEveryDamageHit()
    {
        var game = new Driver(Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] });
        await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0", "E1"], OptionalCosts: ["ECHO"]));
        await game.Top(); await Allocate(game); await Order(game); Assert.Equal(2, game.State.StackItems.Count);
        while (game.State.StackItems.Count > 0) await game.Top(); Assert.Equal(2, Experience(game)); await game.Replay();
    }

    [Theory]
    [InlineData("occurrence")] [InlineData("root")] [InlineData("erase")] [InlineData("duplicate")]
    public async Task RecoveryRejectsReplacingOrErasingTheActualInstructionDeathReceipt(string mutation)
    {
        var game = new Driver(Position(enemies: 1)); await game.Act("P1", new PlayCardCommand("SPELL", Alpha, ["SOURCE", "E0"]));
        await game.Top(); await Order(game); var skill = Assert.Single(game.State.StackItems);
        var origin = skill.UnitEvent!.SpellInstruction!;
        var forged = skill with { UnitEvent = skill.UnitEvent with { SpellInstruction = mutation == "root"
            ? origin with { ParentStackHash = "FORGED-ROOT" } : origin with { Occurrence = "death-1-999" } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = mutation switch {
            "erase" => [], "duplicate" => [skill, skill], _ => [forged] } }));
        await game.Top(); Assert.Equal(1, Experience(game)); await game.Replay();
    }
}
