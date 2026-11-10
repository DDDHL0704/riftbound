using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PrintedPlayAttributeChoiceTests
{
    private const string List = "UNL-138/219", Skill = "EXHAUST_DECLARED_ATTRIBUTE_MINUS_2";
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "LIST", List, "P1", "HAND");
        return RadGearTestPosition.Put(state, "PORO", "UNL-160/219", "P2", "BASE");
    }
    private static async Task Declare(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("LIST", List, []));
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["魄罗"]));
    }

    [Fact]
    public async Task DeclarationIsPartOfPlayBeforePaymentAndEntryAndDoesNotCreateARespondableSkill()
    {
        var game = new Driver(Position()); var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("LIST", List, []));
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECLARE_ATTRIBUTE", choice.Request.Kind); Assert.Equal("P1", choice.Request.PlayerId);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Contains("LIST", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await RadGearTestPosition.Reject(game.State, "P2", new PassPriorityCommand());
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["魄罗"]));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Contains("LIST", game.State.PlayerZones["P1"].Base);
        Assert.Equal("魄罗", Assert.Single(game.State.CardObjects["LIST"].LinkedAttributes!).Attribute);
        Assert.Equal(game.State.CardObjects["LIST"].ObjectGeneration,
            Assert.Single(game.State.CardObjects["LIST"].LinkedAttributes!).Ability.SourceGeneration);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "TRIGGER_QUEUED" or "TRIGGERS_MOVED_TO_STACK");
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["PORO"])); await game.Top();
        Assert.Equal(3, game.State.CardObjects["PORO"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("field-tag")] [InlineData("field-erased")] [InlineData("stack-tag")]
    public async Task RecoveryRejectsASecondOfficialTagOrErasedDeclarationWithoutRewritingAcceptedHistory(string mode)
    {
        var game = new Driver(Position()); await Declare(game);
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["PORO"]));
        var declaration = Assert.Single(game.State.CardObjects["LIST"].LinkedAttributes!);
        var forged = declaration with { Attribute = "盖伦" };
        var state = game.State;
        if (mode == "stack-tag")
        {
            var item = Assert.Single(state.StackItems);
            state = state with { StackItems = [item with { SimpleActivation = item.SimpleActivation! with { LinkedAttribute = forged } }] };
        }
        else state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LIST"] = state.CardObjects["LIST"] with { LinkedAttributes = mode == "field-erased" ? null : [forged] } } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, state), error => error.Contains("original accepted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnaffordablePlayIsRejectedBeforeRequestingADeclaration()
    {
        var state = Position(); state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("LIST", List, []));
    }
}
