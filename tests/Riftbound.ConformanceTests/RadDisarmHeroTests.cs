using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadDisarmHeroTests
{
    public static IEnumerable<object[]> Prints() => new[] { "RAD·063/167", "RAD·109/167", "RAD·109a/167", "RAD·SP3/005" }
        .Select(no => new object[] { no });
    public static IEnumerable<object[]> Copies() => Prints().SelectMany(row => new[] { false, true }.Select(copy => new object[] { row[0], copy }));

    [Theory, MemberData(nameof(Copies))]
    public async Task EachHeroConfirmsOneEnemyAndCopiedDisarmAddsIntoTheSameTrigger(string no, bool copied)
    {
        var initial = AttackDamageDistributionTests.Position(no, copied);
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var skill = Assert.Single(game.State.StackItems); Assert.Equal("DISARM", skill.FieldContext!.Kind);
        Assert.Equal(copied ? 2 : 1, skill.FieldContext.Disarm!.Contributions.Sum(c => c.Value));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.DoesNotContain("HOST", choice.LegalObjectIds); Assert.Contains("ENEMY1", choice.LegalObjectIds);
        Assert.Equal(3, game.State.CardObjects["ENEMY1"].Power);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await game.Top(); Assert.Equal(copied ? 1 : 2, game.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); Assert.Equal(3, game.State.CardObjects["ENEMY2"].Power);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task IllegalTargetsCannotConsumeTheAttackTriggerAndDefendingDoesNotTriggerDisarm(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Position(no));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = game.State.PendingCardChoice!; var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("rad-disarm-self", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"])); await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
        var defending = new Driver(AhriCombatMightTests.Board(no, defending: true));
        await defending.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.DoesNotContain(defending.State.StackItems, i => i.FieldContext?.Kind == "DISARM");
        Assert.DoesNotContain(defending.State.TriggerQueue, i => i.FieldContext?.Kind == "DISARM"); await defending.Replay();
    }

    [Theory]
    [InlineData("RAD·063/167")]
    public async Task KaisaGrantsReactionDuringTheBattleShowdownAndHerConfirmedDisarmResponseWindow(string no)
    {
        var s = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position(no), "SPELL", "VEN·049", "P1");
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·049", [])); await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
    }
}
