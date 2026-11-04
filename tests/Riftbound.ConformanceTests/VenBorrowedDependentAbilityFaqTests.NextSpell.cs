using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedNextSpellPosition()
    {
        var state = BorrowedBombPosition("SFD·078/221");
        foreach (var id in new[] { "FIRST", "SECOND", "THIRD" })
            state = AttackDamageDistributionTests.Spell(state, id, "OGN·048/298", "P1");
        return state;
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task BorrowedNextSpellEchoIsGrantedAtResolutionAndConsumedEvenWhenDeclined(bool echo, bool leave, bool early)
    {
        var initial = BorrowedNextSpellPosition();
        var id = TemplateId(initial, "下一个法术");
        await RadGearTestPosition.Reject(initial, "P1", new PlayCardCommand("FIRST", "OGN·048/298", [], OptionalCosts: ["ECHO"]));
        await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        await RadGearTestPosition.Reject(initial, "P2", new ActivateAbilityCommand("HOST", id, []));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) } },
            "P1", new ActivateAbilityCommand("HOST", id, []));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(initial.RunePools["P1"].TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => EchoCostRules.IsGrant(m, "P1"));
        if (leave || early)
        {
            await game.Act("P1", leave ? new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])
                : new PlayCardCommand("FIRST", "OGN·048/298", []));
            Assert.Equal(1, game.State.StackItems.Last().EffectRepeatCount);
            await game.Top();
        }
        await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, m => EchoCostRules.IsGrant(m, "P1"));
        if (leave) Assert.Equal("HIDDEN", game.Events().Last(e => e.Event.Kind == "NEXT_SPELL_ECHO_GRANTED").Event.Payload["sourceObjectId"]);
        var play = new PlayCardCommand(early ? "SECOND" : "FIRST", "OGN·048/298", [], OptionalCosts: echo ? ["ECHO"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(echo ? 4 : 2, quote.Cost!.Mana);
        await game.Act("P1", play);
        Assert.Equal(echo ? 2 : 1, Assert.Single(game.State.StackItems).EffectRepeatCount);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => EchoCostRules.IsGrant(m, "P1"));
        await game.Top();
        Assert.Equal(3 - (early ? 1 : 0) - (echo ? 2 : 1), game.State.PlayerZones["P1"].MainDeck.Count);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("THIRD", "OGN·048/298", [], OptionalCosts: ["ECHO"]));
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedNextSpellEchoExpiresAtTheEndOfTheGrantingTurn()
    {
        var game = new Driver(BorrowedNextSpellPosition());
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "下一个法术"), []));
        await game.Top();
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => EchoCostRules.IsGrant(m, "P1"));
        await game.Replay();
    }
}
