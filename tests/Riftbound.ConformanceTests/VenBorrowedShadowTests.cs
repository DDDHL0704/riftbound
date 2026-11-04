using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class VenBorrowedShadowTests
{
    private static MatchState Position(string printing = "OGN·111/298", bool spellshield = false)
    {
        var state = Put(AttackDamageDistributionTests.Position("OGN·001/298"), "BORROWER", printing, "P2", "BATTLEFIELD", "F");
        state = Put(state, "DONOR", "UNL-194/219", "P2", "BASE");
        state = Put(state, "OTHER", "OGN·296/298", "P1", "BATTLEFIELD", "OTHER");
        state = Put(state, "OUTSIDE", "OGN·001/298", "P1", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["DONOR"] = state.CardObjects["DONOR"] with { IsExhausted = true },
            ["HOST"] = state.CardObjects["HOST"] with { Tags = spellshield
                ? [..state.CardObjects["HOST"].Tags, CardObjectTags.Spellshield] : state.CardObjects["HOST"].Tags } } };
        state = AttackDamageDistributionTests.Spell(state, "LEAVE", "OGN·104/298", "P2");
        state = AttackDamageDistributionTests.Spell(state, "TARGET_LEAVE", "OGN·104/298", "P1");
        return AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P2");
    }

    private static string Skill(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P2"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "BORROWER")
                && ((string)r["abilityLabel"]!).Contains("进攻此处", StringComparison.Ordinal))
            .Select(r => (string)r["abilityId"]!).Single();

    private static async Task Open(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.True(game.State.CardObjects["BORROWER"].IsDefending);
        await game.Act("P1", new PassFocusCommand());
        Assert.Equal("P2", game.State.FocusPlayerId);
    }

    [Theory]
    [InlineData("OGN·111/298", false)]
    [InlineData("OGN·111/298", true)]
    [InlineData("ARC-003/006", false)]
    [InlineData("ARC-003/006", true)]
    public async Task BorrowedShadowUsesTheBorrowersBattlefieldAndPaysItsCompleteCost(string printing, bool spellshield)
    {
        var game = new Driver(Position(printing, spellshield));
        await Open(game);
        var skill = Skill(game.State);
        foreach (var target in new[] { "ENEMY1", "DONOR", "OUTSIDE", "GEAR", "BORROWER" })
            await Reject(game.State, "P2", new ActivateAbilityCommand("BORROWER", skill, [target]));
        await Reject(game.State, "P1", new ActivateAbilityCommand("BORROWER", skill, ["HOST"]));
        foreach (var pool in new[] { new RunePool(0, 20), new RunePool(20, 0) })
            await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P2"] = pool } },
                "P2", new ActivateAbilityCommand("BORROWER", skill, ["HOST"]));
        var before = game.State.RunePools["P2"];
        await game.Act("P2", new ActivateAbilityCommand("BORROWER", skill, ["HOST"]));
        Assert.True(game.State.CardObjects["BORROWER"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(before.Mana - 1, game.State.RunePools["P2"].Mana);
        Assert.Equal(before.TotalPower - 1 - (spellshield ? 1 : 0), game.State.RunePools["P2"].TotalPower);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("DONOR", item.BorrowedExhaust!.Donor.Object.ObjectId);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust with { BorrowerWasOnBattlefield = false } }] }));
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("donor")]
    [InlineData("source-move")]
    public async Task BorrowedShadowRechecksTheOriginalSourceAndAttackerAfterRealResponses(string response)
    {
        var game = new Driver(Position());
        await Open(game);
        await game.Act("P2", new ActivateAbilityCommand("BORROWER", Skill(game.State), ["HOST"]));
        var actor = response == "target" ? "P1" : "P2";
        if (game.State.PriorityPlayerId != actor) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        var target = response == "target" ? "HOST" : response == "donor" ? "DONOR" : "BORROWER";
        await game.Act(actor, response == "source-move"
            ? new PlayCardCommand("FLASH", "OGS·011/024", [target])
            : new PlayCardCommand(response == "target" ? "TARGET_LEAVE" : "LEAVE", "OGN·104/298", [target]));
        await game.Top();
        await game.Top();
        Assert.Equal(response == "donor", game.State.CardObjects["HOST"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await game.Replay();
    }
}
