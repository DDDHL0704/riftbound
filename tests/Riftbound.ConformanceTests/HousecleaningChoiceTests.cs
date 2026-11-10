using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class HousecleaningChoiceTests
{
    private const string Spell = "OGN·209/298";
    private static MatchState Position(string caster = "P1")
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "SPELL", Spell, caster, "HAND");
        state = RadGearTestPosition.Put(state, "SECOND1", "OGN·219/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SECOND2", "OGN·219/298", "P2", "BASE");
        return state with { TurnPlayerId = caster, ActivePlayerId = caster };
    }
    internal static async Task Choose(Driver game, string player, string objectId)
    {
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(player, choice.Request.PlayerId); Assert.Equal("EACH_PLAYER_DESTROY_OWN_UNIT", choice.Request.Kind);
        await game.Act(player, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [objectId]));
    }

    [Theory, InlineData("P1"), InlineData("P2")]
    public async Task TheSpellHasNoTargetsAndEachPlayerChoosesAndIsResponsibleForTheirOwnUnit(string caster)
    {
        var game = new Driver(Position(caster)); await game.Act(caster, new PlayCardCommand("SPELL", Spell, []));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Top(); var second = caster == "P1" ? "P2" : "P1";
        await Choose(game, caster, caster == "P1" ? "ALLY" : "ENEMY");
        var pending = game.State.PendingRuleChoice!;
        await RadGearTestPosition.Reject(game.State, caster, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SECOND1"]));
        await RadGearTestPosition.Reject(game.State, second, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT",
            [second == "P1" ? "SECOND2" : "SECOND1"]));
        await Choose(game, second, second == "P1" ? "ALLY" : "ENEMY");
        Assert.Contains("SPELL", game.State.PlayerZones[caster].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("SECOND1", game.State.PlayerZones["P1"].Base); Assert.Contains("SECOND2", game.State.PlayerZones["P2"].Base);
        foreach (var death in game.Events().Where(entry => entry.Event.Kind == "UNIT_DESTROYED"))
        {
            var expected = Equals(death.Event.Payload["targetObjectId"], "ALLY") ? "P1" : "P2";
            Assert.Equal(expected, death.Event.Payload["destroyedByPlayerId"]);
            var cause = Assert.IsType<DestructionAttribution>(death.Event.Payload["destructionAttribution"]);
            Assert.Equal(caster, cause.ControllerId); Assert.Equal(expected, cause.ResponsiblePlayerId); Assert.True(cause.FromSpell);
        }
        await game.Replay();
    }

    [Fact]
    public async Task AClientCannotSelectEitherPlayersUnitWhenCastingTheSpell()
        => await RadGearTestPosition.Reject(Position(), "P1", new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"]));

    [Theory, InlineData("P1"), InlineData("P2"), InlineData("both")]
    public async Task APlayerWithoutAnyUnitDoesNotPreventTheOtherInstructionOrTheSpellFromCompleting(string empty)
    {
        var state = Position();
        if (empty is "P1" or "both") foreach (var id in new[] { "ALLY", "SECOND1" }) state = RadUnitEffectTestFixture.Place(state, id, "P1", "HAND");
        if (empty is "P2" or "both") foreach (var id in new[] { "ENEMY", "SECOND2" }) state = RadUnitEffectTestFixture.Place(state, id, "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SPELL", Spell, [])); await game.Top();
        if (empty == "P1") await Choose(game, "P2", "ENEMY");
        if (empty == "P2") await Choose(game, "P1", "ALLY");
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task EachPlayerMayPickTheirOwnUnitWithEnemyTargetProtection()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY", "UNL-147/219", "P2", "BASE");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SPELL", Spell, [])); await game.Top();
        await Choose(game, "P1", "ALLY"); await Choose(game, "P2", "ENEMY");
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }
    [Fact]
    public async Task GrantedEchoRepeatsEachPlayersChoiceAndKeepsTheSourceOnTheStackUntilTheLastExecution()
    {
        var state = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, [], OptionalCosts: ["ECHO"], RepeatChoices: [new("", [])]));
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); await game.Top();
        await Choose(game, "P1", "ALLY"); await Choose(game, "P2", "ENEMY");
        var second = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P1", second.Request.PlayerId);
        var preview = Assert.IsType<MatchState>(second.Request.Preview);
        Assert.DoesNotContain("SPELL", preview.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", preview.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", preview.PlayerZones["P2"].Graveyard);
        await Choose(game, "P1", "SECOND1"); await Choose(game, "P2", "SECOND2");
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, game.Events().Count(entry => entry.Event.Kind == "UNIT_DESTROYED")); await game.Replay();
    }

    [Fact]
    public void QuotesAndPromptsOfferTheUntargetedSpellEvenWhenBothPlayersHaveNoUnits()
    {
        var state = Position();
        foreach (var id in new[] { "ALLY", "SECOND1" }) state = RadUnitEffectTestFixture.Place(state, id, "P1", "HAND");
        foreach (var id in new[] { "ENEMY", "SECOND2" }) state = RadUnitEffectTestFixture.Place(state, id, "P2", "HAND");
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1",
            PlayCostPreviewTests.Request(state, new PlayCardCommand("SPELL", Spell, [])));
        Assert.True(quote.CanPay, quote.Message);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [], item => item.Action == CommandTypes.PlayCard
            && item.Sources?.Any(source => source.Id == "SPELL") == true);
        Assert.Empty(candidate.Targets ?? []); Assert.Equal(before, MatchStateHasher.Hash(state));
    }

}
