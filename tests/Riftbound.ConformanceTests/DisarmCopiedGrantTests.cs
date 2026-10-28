using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DisarmTriggerTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DisarmCopiedGrantTests
{
    [Theory]
    [InlineData("stays", 2)]
    [InlineData("leaves-before-role", 1)]
    [InlineData("leaves-after-confirmation", 2)]
    public async Task ARealCopiedPatrolContributesAtAttackAndItsEarnedGrantSurvivesDeparture(string mode, int amount)
    {
        const string tokenId = "COPY-TOKEN-001";
        var row = "copied-patrol-" + mode;
        var initial = OfficialCopyIdentityTests.AddSpell(await Position("RAD·055/167", 0), "COPY", "UNL-200/219");
        initial = AttackDamageDistributionTests.Spell(initial, "SOUL", "SFD·163/221", "P1");
        var copied = await OfficialCopyIdentityTests.Copy(initial, "HOST", "COPY");
        var token = copied.State.CardObjects[tokenId];
        Assert.Equal("RAD·055/167", token.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, token.TokenFactoryCardNo);
        Assert.True(TokenObjectRules.IsToken(token));
        Assert.Contains("机械", token.Tags);
        Assert.Contains("COPY", copied.State.PlayerZones["P1"].Graveyard);
        Record(row, "copy-resolved", copied.State, copied.Events);
        Restore(copied.State); ArmamentMightTests.PublicRecovery(copied.State);

        var beforeAttack = copied.State;
        if (mode == "leaves-before-role") beforeAttack = (await Sacrifice(beforeAttack, "before-role")).State;
        var attack = await Attack(beforeAttack);
        var contributions = Assert.Single(attack.State.StackItems).FieldContext!.Disarm!.Contributions;
        Assert.Equal(amount, contributions.Sum(c => c.Amount));
        Assert.Equal(mode == "leaves-before-role" ? 0 : 1, contributions.Count(c => c.Source.ObjectId == tokenId));
        Record(row, "attack", attack.State, attack.Events);
        var confirmed = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1");
        if (mode == "leaves-after-confirmation") confirmed = await Sacrifice(confirmed.State, "after-confirmation");
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Equal(3 - amount, Might(done.State));
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Record(row, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);

        async Task<ResolutionResult> Sacrifice(MatchState state, string stage)
        {
            var cast = await Act(state, "P1", new PlayCardCommand("SOUL", "SFD·163/221", [tokenId, "HOST"]));
            Record(row, stage + "-cast", cast.State, cast.Events);
            var departed = await TopWithReceipt(cast.State, row + "-" + stage);
            Assert.False(departed.State.CardObjects.ContainsKey(tokenId));
            Assert.Contains(departed.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
            foreach (var zones in departed.State.PlayerZones.Values)
                Assert.DoesNotContain(tokenId, zones.Hand.Concat(zones.MainDeck).Concat(zones.Graveyard).Concat(zones.Banished));
            Record(row, stage + "-departed", departed.State, departed.Events);
            return departed;
        }
    }
}
