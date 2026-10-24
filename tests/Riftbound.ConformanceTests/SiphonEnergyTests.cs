using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SiphonEnergyTests
{
    internal const string Spell = "OGN·266/298";
    internal static PlayCardCommand Command(params string[] targets) => new("AOE", Spell, targets);
    internal static async Task<MatchState> Board()
    {
        return (await Act(BeforeBattle(), "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []))).State;
    }

    internal static MatchState BeforeBattle()
    {
        var s = MoonfallTests.BeforeBattle();
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["AOE"] = s.CardObjects["AOE"] with { CardNo = Spell } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } };
    }

    [Fact]
    public async Task ChosenBattlefieldIsRequiredAndOtherBattlefieldsAreUnaffected()
    {
        var s = await Board();
        var done = await Top((await Act(s, "P1", new PlayCardCommand("AOE", Spell, ["BF"]))).State);
        Assert.Equal(s.CardObjects["A"].Power + 1, done.State.CardObjects["A"].Power);
        Assert.Equal(3, done.State.CardObjects["D"].Power);
        Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.Equal(4, done.State.CardObjects["D3"].Power);
        Restore(done.State);
    }

    [Theory]
    [InlineData("BF2")]
    [InlineData("BF2", true)]
    public async Task EnemyOnlyAndEmptyBattlefieldsCanBeChosen(string field, bool empty = false)
    {
        var s = await Board();
        if (empty) s = At(s, "D2", "P2", "BASE");
        var done = await Top((await Act(s, "P1", Command(field))).State);
        Assert.Equal(s.CardObjects["A"].Power, done.State.CardObjects["A"].Power);
        Assert.Equal(4, done.State.CardObjects["D"].Power);
        Assert.Equal(empty ? 4 : 3, done.State.CardObjects["D2"].Power);
        Assert.Equal(4, done.State.CardObjects["D3"].Power);
        Restore(done.State);
    }

    [Theory]
    [InlineData("")] [InlineData("D")] [InlineData("D3")] [InlineData("UNKNOWN")]
    public async Task MissingOrNonBattlefieldChoiceCannotPayOrChangeState(string target)
    {
        var s = await Board();
        var command = Command(target.Length == 0 ? [] : [target]);
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", new("invalid", prompt.PromptId!, s.Tick, command));
        Assert.False(quote.IsValid);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("invalid", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task GroupMembersAreAffectedWithoutBeingWardTargetsAndFloorDoesNotIncreaseZero()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { Power = 0, Tags = [CardObjectTags.UnitCard, "法盾3"] } } };
        var before = s.RunePools["P1"].TotalPower;
        var done = await Top((await Act(s, "P1", Command("BF"))).State);
        Assert.Equal(before - 1, done.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, done.State.CardObjects["D"].Power);
        Assert.Empty(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Assert.Contains(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN" && Equals(e.Payload["targetObjectId"], "D") && Equals(e.Payload["appliedPowerDelta"], 0));
        Restore(done.State);
    }

    [Fact]
    public async Task EchoCanChooseSeparateBattlefieldsWithoutSpreadingEitherEffect()
    {
        var s = await Board();
        s = s with { UntilEndOfTurnEffects = [..s.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P1"] };
        var command = Command("BF") with { OptionalCosts = ["ECHO"], RepeatChoices = [new("", ["BF2"])] };
        var done = await Top((await Act(s, "P1", command)).State);
        Assert.Equal(s.CardObjects["A"].Power + 1, done.State.CardObjects["A"].Power);
        Assert.Equal(3, done.State.CardObjects["D"].Power);
        Assert.Equal(3, done.State.CardObjects["D2"].Power);
        Assert.Equal(4, done.State.CardObjects["D3"].Power);
        Restore(done.State);
    }

    [Fact]
    public async Task ResolutionCollectsTheUnitsCurrentlyAtTheChosenBattlefield()
    {
        var s = await Board();
        var cast = await Act(s, "P1", Command("BF2"));
        // Seed only the response's resulting locations; these are affected
        // group members, not declared targets or new choices for the spell.
        var response = At(At(cast.State, "D2", "P2", "BASE"), "D3", "P2", "BATTLEFIELD", "BF2");
        var done = await Top(response);
        Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.Equal(3, done.State.CardObjects["D3"].Power);
        Assert.Equal(s.CardObjects["A"].Power, done.State.CardObjects["A"].Power);
        Assert.Equal(4, done.State.CardObjects["D"].Power);
        Restore(done.State);
    }

    [Theory]
    [InlineData(-2, -1)] [InlineData(0, 1)]
    public async Task FriendlyIncreaseKeepsItsActualArithmeticBelowOne(int before, int after)
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["A"] = s.CardObjects["A"] with { Power = before } } };
        var done = await Top((await Act(s, "P1", Command("BF"))).State);
        Assert.Equal(after, done.State.CardObjects["A"].Power);
        var modifier = Assert.Single(done.State.CardObjects["A"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(1, modifier.PowerDelta); Assert.Equal(0, modifier.MinimumPower);
        Assert.Null(modifier.SnapshotMight);
        Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
    }

    [Fact]
    public async Task ReturnedBattlefieldDoesNotReplaceTheOriginalTargetGeneration()
    {
        var cast = await Act(await Board(), "P1", Command("BF2"));
        var changed = cast.State with { CardObjects = new Dictionary<string, CardObjectState>(cast.State.CardObjects) {
            ["BF2"] = cast.State.CardObjects["BF2"] with { ObjectGeneration = 1 } } };
        var done = await Top(changed);
        Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Restore(done.State);
    }
}
