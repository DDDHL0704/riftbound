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
        Restore(copied.State); ArmamentMightTests.PublicRecovery(copied.State);

        var beforeAttack = copied.State;
        if (mode == "leaves-before-role") beforeAttack = (await Sacrifice(beforeAttack, "before-role")).State;
        var attack = await Attack(beforeAttack);
        var contributions = Assert.Single(attack.State.StackItems).FieldContext!.Disarm!.Contributions;
        Assert.Equal(amount, contributions.Sum(c => c.Amount));
        Assert.Equal(mode == "leaves-before-role" ? 0 : 1, contributions.Count(c => c.Source.ObjectId == tokenId));
        var confirmed = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1");
        if (mode == "leaves-after-confirmation") confirmed = await Sacrifice(confirmed.State, "after-confirmation");
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Equal(3 - amount, Might(done.State));
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);

        async Task<ResolutionResult> Sacrifice(MatchState state, string stage)
        {
            var cast = await Act(state, "P1", new PlayCardCommand("SOUL", "SFD·163/221", [tokenId, "HOST"]));
            var departed = await TopWithReceipt(cast.State, row + "-" + stage);
            Assert.False(departed.State.CardObjects.ContainsKey(tokenId));
            Assert.Contains(departed.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
            foreach (var zones in departed.State.PlayerZones.Values)
                Assert.DoesNotContain(tokenId, zones.Hand.Concat(zones.MainDeck).Concat(zones.Graveyard).Concat(zones.Banished));
            return departed;
        }
    }

    [Theory]
    [InlineData("capture-future")]
    [InlineData("capture-negative")]
    [InlineData("face-revision-mismatch")]
    [InlineData("factory-missing")]
    [InlineData("factory-unknown")]
    [InlineData("ceased-missing")]
    [InlineData("ceased-source")]
    [InlineData("ceased-generation")]
    [InlineData("ceased-factory")]
    [InlineData("ceased-face")]
    [InlineData("ceased-face-revision")]
    [InlineData("ceased-capture-tick")]
    [InlineData("ceased-early")]
    [InlineData("ceased-future")]
    [InlineData("source-present")]
    public async Task ColdRecoveryRejectsContradictoryCopiedGrantDepartureProof(string change)
    {
        const string tokenId = "COPY-TOKEN-001";
        var row = "copied-grant-proof-" + change;
        var initial = OfficialCopyIdentityTests.AddSpell(await Position("RAD·055/167", 0), "COPY", "UNL-200/219");
        initial = AttackDamageDistributionTests.Spell(initial, "SOUL", "SFD·163/221", "P1");
        var copied = await OfficialCopyIdentityTests.Copy(initial, "HOST", "COPY");
        var token = copied.State.CardObjects[tokenId];
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(copied.State)).State, "ENEMY1");
        var cast = await Act(confirmed.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", [tokenId, "HOST"]));
        var departed = await TopWithReceipt(cast.State, row + "-settle");
        Assert.False(departed.State.CardObjects.ContainsKey(tokenId));
        var item = Assert.Single(departed.State.StackItems);
        var context = item.FieldContext!;
        var disarm = context.Disarm!;
        var contribution = Assert.Single(disarm.Contributions, c => c.Source.ObjectId == tokenId);
        var ceased = Assert.IsType<CeasedDisarmSource>(contribution.CeasedSource);
        Assert.Equal(new ObjectBinding(tokenId, token.ObjectGeneration), ceased.Source);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, ceased.FactoryCardNo);
        Assert.Equal("RAD·055/167", ceased.CardNo);

        var changed = change switch
        {
            "face-revision-mismatch" => contribution with { FaceRevision = contribution.FaceRevision + 1 },
            "factory-missing" => contribution with { SourceTokenFactoryCardNo = null },
            "factory-unknown" => contribution with { SourceTokenFactoryCardNo = "UNKNOWN-FACTORY",
                CeasedSource = ceased with { FactoryCardNo = "UNKNOWN-FACTORY" } },
            "ceased-missing" => contribution with { CeasedSource = null },
            "ceased-source" => contribution with { CeasedSource = ceased with { Source = new("HOST", ceased.Source.Generation) } },
            "ceased-generation" => contribution with { CeasedSource = ceased with { Source = ceased.Source with { Generation = ceased.Source.Generation + 1 } } },
            "ceased-factory" => contribution with { CeasedSource = ceased with { FactoryCardNo = "OGN·273/298" } },
            "ceased-face" => contribution with { CeasedSource = ceased with { CardNo = "RAD·054/167" } },
            "ceased-face-revision" => contribution with { CeasedSource = ceased with { FaceRevision = ceased.FaceRevision + 1 } },
            "ceased-capture-tick" => contribution with { CeasedSource = ceased with { CapturedTick = ceased.CapturedTick + 1 } },
            "ceased-early" => contribution with { CeasedSource = ceased with { Tick = disarm.CapturedTick - 1 } },
            "ceased-future" => contribution with { CeasedSource = ceased with { Tick = departed.State.Tick + 1 } },
            "capture-future" => contribution with { CeasedSource = ceased with {
                CapturedTick = departed.State.Tick + 1, Tick = departed.State.Tick + 1 } },
            _ => contribution
        };
        var changedContext = disarm with { Contributions = disarm.Contributions.Select(c => c == contribution ? changed : c).ToArray() };
        if (change == "capture-future") changedContext = changedContext with { CapturedTick = departed.State.Tick + 1 };
        if (change == "capture-negative") changedContext = changedContext with { CapturedTick = -1 };
        var forged = departed.State with { StackItems = [item with { FieldContext = context with { Disarm = changedContext } }] };
        if (change == "source-present")
        {
            forged = forged with {
                CardObjects = new Dictionary<string, CardObjectState>(forged.CardObjects) { [tokenId] = token },
                PlayerZones = new Dictionary<string, PlayerZones>(forged.PlayerZones) {
                    ["P1"] = forged.PlayerZones["P1"] with { Base = [..forged.PlayerZones["P1"].Base, tokenId] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(forged.ObjectLocations) { [tokenId] = new("P1", "BASE") }
            };
        }
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("field trigger stack context", StringComparison.Ordinal));
        Restore(departed.State); ArmamentMightTests.PublicRecovery(departed.State);
    }
}
