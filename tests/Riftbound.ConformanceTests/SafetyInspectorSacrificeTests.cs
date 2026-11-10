using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SafetyInspectorSacrificeTests
{
    private const string Inspector = "UNL-164/219", Extra = "SPEND_EXPERIENCE:3";
    private static MatchState Position(int experience = 3)
        => RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "UNIT", Inspector, "P1", "HAND") with {
            PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 20 } };
    private static PlayCardCommand Play(bool pay) => new("UNIT", Inspector, [], OptionalCosts: pay ? [Extra] : []);
    private static async Task Select(Driver game, string player, string unit)
    {
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(player, choice.Request.PlayerId); Assert.Equal("EACH_PLAYER_DESTROY_OWN_UNIT", choice.Request.Kind);
        await game.Act(player, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [unit]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EachPlayerChoosesOnlyDuringResolutionAndPaidExperienceExemptsOnlyTheOriginalController(bool pay)
    {
        var game = new Driver(Position()); var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", Play(pay));
        Assert.Equal(pay ? 0 : 3, game.State.PlayerExperience["P1"]); Assert.Equal(mana - 5, game.State.RunePools["P1"].Mana);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(pay, skill.UnitEvent!.SourceOptionalExperiencePaid);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Top();
        if (!pay) await Select(game, "P1", "ALLY");
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ENEMY"]));
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["UNIT"]));
        await Select(game, "P2", "ENEMY");
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(!pay, game.State.PlayerZones["P1"].Graveyard.Contains("ALLY")); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        var deaths = game.Events().Where(entry => entry.Event.Kind == "UNIT_DESTROYED").ToArray(); Assert.Equal(pay ? 1 : 2, deaths.Length);
        foreach (var death in deaths)
            Assert.Equal(death.Event.Payload["targetObjectId"] is "ENEMY" ? "P2" : "P1", death.Event.Payload["destroyedByPlayerId"]);
        Assert.Equal(pay ? 0 : 3, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task AnUnpaidInspectorMayBeItsControllersOnlyUnitAndItsIndependentInstructionStillReachesTheOpponent()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "HAND"));
        await game.Act("P1", Play(false)); await game.Top(); await Select(game, "P1", "UNIT"); await Select(game, "P2", "ENEMY");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceCannotCancelItsPaidExemptionOrRebindTheDestructionToItsPrivateGeneration()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", Play(true)); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Top(); await Select(game, "P2", "ENEMY");
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Equal(0, game.State.PlayerExperience["P1"]);
        var death = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED");
        var attribution = Assert.IsType<DestructionAttribution>(death.Event.Payload["destructionAttribution"]);
        Assert.Equal(generation, attribution.Source.Generation); Assert.Equal("P1", attribution.ControllerId);
        Assert.Equal("P2", attribution.ResponsiblePlayerId); Assert.False(attribution.FromSpell); await game.Replay();
    }

    [Fact]
    public async Task TheOpponentsChoiceIsUntargetedAndCanDestroyItsOwnProtectedBaron()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "ENEMY", "UNL-147/219", "P2", "BASE"));
        await game.Act("P1", Play(true)); await game.Top(); await Select(game, "P2", "ENEMY");
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentWithoutUnitsDoesNotPreventThePaidInstructionFromCompleting()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "HAND"));
        await game.Act("P1", Play(true)); await game.Top(); Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("wrong")] [InlineData("duplicate")] [InlineData("insufficient")]
    public async Task InvalidAdditionalCostsCannotSpendResourcesOrStartEntry(string mode)
    {
        var costs = mode == "wrong" ? new[] { "SPEND_EXPERIENCE:4" } : mode == "duplicate" ? [Extra, Extra] : [Extra];
        await RadGearTestPosition.Reject(Position(mode == "insufficient" ? 2 : 6), "P1", Play(false) with { OptionalCosts = costs });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsChangingTheOriginallyCapturedPaidChoiceInEitherDirection(bool paid)
    {
        var game = new Driver(Position()); await game.Act("P1", Play(paid)); var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = [item with { UnitEvent = item.UnitEvent! with { SourceOptionalExperiencePaid = !paid } }] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error => error.Contains("original accepted capture", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecoveryRejectsErasingTheEarnedInstructionAndClientTargetsAreRejectedBeforeEntry()
    {
        await RadGearTestPosition.Reject(Position(), "P1", Play(false) with { TargetObjectIds = ["ENEMY"] });
        var game = new Driver(Position()); await game.Act("P1", Play(false));
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [] }),
            error => error.Contains("original accepted completion", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, false)] [InlineData(3, true)]
    public void QuotesAndPromptsOfferTheExperienceChoiceWithoutAdvertisingAManaDiscount(int experience, bool available)
    {
        var state = Position(experience); var hash = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Play(true)));
        Assert.Equal(available, quote.CanPay); Assert.Equal(3, quote.Cost!.Experience); Assert.Equal(5, quote.Cost.Mana);
        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [], candidate => candidate.Action == CommandTypes.PlayCard
            && candidate.Sources?.Any(source => source.Id == "UNIT") == true);
        Assert.Equal(available, play.OptionalCosts?.Any(cost => cost.Id == Extra) == true);
        if (available) Assert.Contains("无需摧毁", Assert.Single(play.OptionalCosts!, cost => cost.Id == Extra).Label);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }
}
