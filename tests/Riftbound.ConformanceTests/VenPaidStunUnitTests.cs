using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenPaidStunUnitTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ThunderfistPaysOptionalYellowBeforeChoosingAnEnemyBattlefieldUnit(bool pay)
    {
        var initial = Position(); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: pay ? ["SPEND_POWER:yellow:1"] : []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(16, game.State.RunePools["P1"].Mana); Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        if (!pay) { Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay(); return; }
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        foreach (var id in new[] { "UNIT", "ALLY", "GEAR", "GRAVE" })
            await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Act("P1", new EndTurnCommand()); Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task WrongColorOrAmountCannotPayAndBaseEnemiesCannotBeChosen()
    {
        var initial = Position();
        foreach (var cost in new[] { "SPEND_POWER:blue:1", "SPEND_POWER:yellow:2" })
            await Reject(initial, new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: [cost]));
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 0, new Dictionary<string, int> { ["yellow"] = 1 }) } },
            new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: ["SPEND_POWER:yellow:1"]));
        var noBattlefieldTarget = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        var game = new Driver(noBattlefieldTarget);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: ["SPEND_POWER:yellow:1"]));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task EnemyReturningToHandInResponseInvalidatesTheConfirmedStun()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "BOUNCE", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: ["SPEND_POWER:yellow:1"]));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BOUNCE", "OGN·104/298", ["ENEMY"])); await game.Top();
        await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await game.Replay();
    }

    private static MatchState Position()
    {
        var state = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position("VEN·120"), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { ["yellow"] = 1 }) } };
    }
    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-paid-stun", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
