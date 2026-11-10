using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ForcedConscriptionExperienceTests
{
    private const string Conscription = "UNL-140/219", Extra = "SPEND_EXPERIENCE:5";
    private static MatchState Position(int experience = 5, bool small = false)
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "SPELL", Conscription, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "TARGET", small ? "OGN·010/298" : "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        return state with { PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 20 } };
    }
    private static PlayCardCommand Command(bool pay, string target = "TARGET")
        => new("SPELL", Conscription, [target], OptionalCosts: pay ? [Extra] : []);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualPlayPaysOptionalExperienceOnceWithoutChangingBaseManaAndPower(bool pay)
    {
        var game = new Driver(Position(5, !pay));
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", Command(pay));
        Assert.Equal(pay ? 0 : 5, game.State.PlayerExperience["P1"]);
        Assert.Equal(mana - 5, game.State.RunePools["P1"].Mana); Assert.Equal(power - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("P2", game.State.CardObjects["TARGET"].ControllerId); await game.Top();
        var target = game.State.CardObjects["TARGET"];
        Assert.Equal("P2", target.OwnerId); Assert.Equal("P1", target.ControllerId); Assert.True(target.IsExhausted);
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Base); Assert.Equal(pay ? 0 : 5, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(0, false)] [InlineData(5, false)] [InlineData(4, true)]
    public async Task AHighMightTargetRequiresTheExactAdditionalCostAndEnoughCurrentExperience(int experience, bool pay)
        => await RadGearTestPosition.Reject(Position(experience), "P1", Command(pay));

    [Theory]
    [InlineData("wrong")] [InlineData("duplicate")]
    public async Task WrongOrDuplicateExperienceSelectionsAreRejectedWithoutDebitingAnything(string mode)
    {
        var costs = mode == "wrong" ? new[] { "SPEND_EXPERIENCE:4" } : [Extra, Extra];
        await RadGearTestPosition.Reject(Position(10, true), "P1", Command(false) with { OptionalCosts = costs });
    }

    [Theory]
    [InlineData("base")] [InlineData("friendly")] [InlineData("protected")]
    public async Task PayingExperienceRemovesOnlyTheMightLimitAndPreservesOtherTargetRestrictions(string mode)
    {
        var state = mode == "protected" ? RadGearTestPosition.Put(Position(), "TARGET", "UNL-147/219", "P2", "BATTLEFIELD", "OTHER")
            : mode == "base" ? RadUnitEffectTestFixture.Place(Position(), "TARGET", "P2", "BASE")
            : RadGearTestPosition.Put(Position(), "TARGET", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        await RadGearTestPosition.Reject(state, "P1", Command(true));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealResponseBuffRechecksAnUnpaidLimitButCannotRestoreAPaidLimit(bool pay)
    {
        var state = RadGearTestPosition.Put(Position(5, true), "BUFF", "OGN·058/298", "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", Command(pay)); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BUFF", "OGN·058/298", ["TARGET"])); await game.Top();
        Assert.Equal(4, game.State.CardObjects["TARGET"].Power); await game.Top();
        Assert.Equal(pay ? "P1" : "P2", game.State.CardObjects["TARGET"].ControllerId);
        Assert.Equal(pay ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["TARGET"].Zone);
        Assert.Equal(pay ? 0 : 5, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryBindsTheOptionalChoiceToTheOriginalAcceptedPaymentEvenForASmallTarget(bool paid)
    {
        var game = new Driver(Position(5, true)); await game.Act("P1", Command(paid));
        var item = Assert.Single(game.State.StackItems);
        var errors = ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = [item with { OptionalCosts = paid ? [] : [Extra] }] });
        Assert.Contains(errors, error => error.Contains("original accepted payment", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExperienceComposesWithGrantedEchoInEitherOrderAndIsPaidOnceForBothHighMightTargets(bool reverse)
    {
        var state = RadGearTestPosition.Put(Position(), "SECOND", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = state with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var command = Command(true) with { OptionalCosts = reverse ? ["ECHO", Extra] : [Extra, "ECHO"],
            RepeatChoices = [new("", ["SECOND"])] };
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(5, quote.Cost!.Experience);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); Assert.Equal(0, game.State.PlayerExperience["P1"]);
        await game.Top();
        Assert.All(new[] { "TARGET", "SECOND" }, id => {
            Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
        }); Assert.Equal(0, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(4, false)] [InlineData(5, true)]
    public void QuotesAndPromptsExposeTheAdditionalCostOnlyWithEnoughExperience(int experience, bool available)
    {
        var state = Position(experience); var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Command(true)));
        Assert.Equal(available, quote.CanPay); Assert.NotNull(quote.Cost); Assert.Equal(5, quote.Cost.Experience);
        var candidates = ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [];
        var playable = candidates.Where(candidate => candidate.Action == CommandTypes.PlayCard
            && candidate.Sources?.Any(source => source.Id == "SPELL") == true).ToArray();
        if (available)
        {
            var play = Assert.Single(playable);
            Assert.Contains(play.OptionalCosts ?? [], cost => cost.Id == Extra);
            Assert.Contains(play.Targets ?? [], target => target.Id == "TARGET");
        }
        else Assert.Empty(playable); // Its only enemy battlefield target is too large without the extra cost.
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }
}
