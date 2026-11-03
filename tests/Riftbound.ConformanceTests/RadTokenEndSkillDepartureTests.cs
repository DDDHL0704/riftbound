using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTokenEndSkillDepartureTests
{
    private const string Sand = "SFD·031/221", Image = "UNL-200/219";
    private const string Return = "OGN·104/298", Destroy = "SFD·163/221";

    private static MatchState Position(string spell)
    {
        var state = VenUnitEntryBasicTests.Position("OGN·219/298");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
        state = HandDiscardObserverTests.Put(state, "MAKE", spell, "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "RETURN", Return, "P1", "HAND");
        return HandDiscardObserverTests.Put(state, "DESTROY", Destroy, "P1", "HAND");
    }

    private static async Task<string> CreateMarkedToken(Driver game, string spell)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("MAKE", spell, spell == Image ? ["ALLY"] : []));
        await game.Top();
        for (var step = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } pending; step++)
        {
            Assert.True(step < 2);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        if (spell == Image) await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null);
        Assert.Single(token.UntilEndOfTurnEffects, effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        return token.ObjectId;
    }

    private static void AssertCeased(Driver game, string token)
    {
        Assert.False(game.State.CardObjects.ContainsKey(token));
        Assert.False(game.State.ObjectLocations.ContainsKey(token));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TOKEN_CEASED_TO_EXIST"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), token));
        if (game.State.StackItems.SingleOrDefault(item => item.EndAbility is not null) is { } skill)
        {
            var context = skill.EndAbility!;
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(context.CeasedSource);
            foreach (var forged in new[] {
                context with { CapturedTick = null },
                context with { CeasedSource = null },
                context with { Source = context.Source with { TokenFactoryCardNo = "UNKNOWN-FACTORY" } },
                context with { CeasedSource = receipt with { TokenFactoryCardNo = "UNKNOWN-FACTORY" } },
                context with { CeasedSource = receipt with { Source = new("LEGEND", 0) } },
                context with { CeasedSource = receipt with { DepartedTick = context.CapturedTick!.Value - 1 } } })
            {
                var bad = game.State with { StackItems = game.State.StackItems.Select(item => item.StackItemId == skill.StackItemId
                    ? item with { EndAbility = forged } : item).ToArray() };
                Assert.Contains("invalid end-step ability", MatchRecoveryValidator.ValidateComponentStateAndViews(bad));
            }
        }
    }

    [Theory]
    [InlineData(Sand, false)]
    [InlineData(Sand, true)]
    [InlineData(Image, false)]
    [InlineData(Image, true)]
    public async Task ReturningATokenBeforeOrAfterItsEndSkillIsCapturedLeavesNoDanglingSkill(string spell, bool duringEndStep)
    {
        var game = new Driver(Position(spell));
        var token = await CreateMarkedToken(game, spell);
        if (duringEndStep)
        {
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(token, Assert.Single(game.State.StackItems).SourceObjectId);
        }
        await game.Act("P1", new PlayCardCommand("RETURN", Return, [token]));
        await game.Top();
        AssertCeased(game, token);
        if (duringEndStep) await game.Top();
        else await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), token));
        await game.Replay();
    }

    [Fact]
    public async Task DestroyingATokenInResponseDoesNotRepeatItsDeathOrStrandItsCapturedEndSkill()
    {
        var game = new Driver(Position(Sand));
        var token = await CreateMarkedToken(game, Sand);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(token, Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Act("P1", new PlayCardCommand("DESTROY", Destroy, [token, "ALLY"]));
        await game.Top();
        AssertCeased(game, token);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), token));
        await game.Replay();
    }
}
