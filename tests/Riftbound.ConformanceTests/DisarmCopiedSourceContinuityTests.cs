using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DisarmCopiedSourceContinuityTests
{
    [Theory]
    [InlineData("stays", 2)]
    [InlineData("return-before-attack", 1)]
    [InlineData("destroy-before-attack", 1)]
    [InlineData("return-after-confirmation", 2)]
    [InlineData("destroy-after-confirmation", 2)]
    public async Task ARealCopiedPatrollerContributesAtAttackAndItsCapturedGrantSurvivesDeparture(
        string scenario, int expectedAmount)
    {
        const string tokenId = "COPY-TOKEN-001";
        var initial = OfficialCopyIdentityTests.AddSpell(RadDisarmUnitTests.Board("RAD·055/167"),
            "COPY", "UNL-200/219");
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "SOUL", "SFD·163/221", "P1");
        // All commands share one journal. Cold checkpoints retain complete
        // original replay at creation, confirmation, departure and resolution.
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["HOST"]));
        await game.Top();
        await CloseEmptyDuel();
        var token = game.State.CardObjects[tokenId];
        Assert.Equal("RAD·055/167", token.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, token.TokenFactoryCardNo);
        Assert.True(TokenObjectRules.IsToken(token));
        Assert.Contains("机械", token.Tags);
        Assert.Contains("COPY", game.State.PlayerZones["P1"].Graveyard);
        await game.ColdCheckpoint();

        if (scenario.EndsWith("before-attack", StringComparison.Ordinal)) await Depart();
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var skill = Assert.Single(game.State.StackItems);
        var contributions = skill.FieldContext!.Disarm!.Contributions;
        Assert.Equal(expectedAmount, contributions.Sum(contribution => contribution.Value));
        Assert.Equal(scenario.EndsWith("before-attack", StringComparison.Ordinal) ? 0 : 1,
            contributions.Count(contribution => contribution.Source.ObjectId == tokenId));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await game.ColdCheckpoint();
        if (scenario.EndsWith("after-confirmation", StringComparison.Ordinal))
        {
            await Depart();
            var remaining = Assert.Single(game.State.StackItems);
            Assert.Equal(skill.StackItemId, remaining.StackItemId);
            Assert.Equal(expectedAmount, remaining.FieldContext!.Disarm!.Contributions.Sum(contribution => contribution.Value));
        }

        await game.Top();
        Assert.Equal(initial.CardObjects["ENEMY1"].Power - expectedAmount, game.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(scenario == "stays", game.State.CardObjects.ContainsKey(tokenId));
        await game.ColdCheckpoint();

        async Task Depart()
        {
            var firstEvent = game.Events().Length;
            if (scenario.StartsWith("return-", StringComparison.Ordinal))
                await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [tokenId]));
            else
                await game.Act("P1", new PlayCardCommand("SOUL", "SFD·163/221", [tokenId, "HOST"]));
            await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(tokenId));
            Assert.False(game.State.ObjectLocations.ContainsKey(tokenId));
            Assert.Single(game.Events().Skip(firstEvent), recovered => recovered.Event.Kind == "TOKEN_CEASED_TO_EXIST"
                && Equals(recovered.Event.Payload.GetValueOrDefault("targetObjectId"), tokenId));
            foreach (var zones in game.State.PlayerZones.Values)
                Assert.DoesNotContain(tokenId, zones.Base.Concat(zones.Battlefields).Concat(zones.Hand)
                    .Concat(zones.MainDeck).Concat(zones.Graveyard).Concat(zones.Banished));
            await game.ColdCheckpoint();
            await CloseEmptyDuel();
        }

        async Task CloseEmptyDuel()
        {
            for (var passes = 0; game.State.StackItems.Count == 0 && game.State.FocusPlayerId is { } player; passes++)
            {
                Assert.True(passes < 4, "An empty spell duel must close after both players pass focus.");
                await game.Act(player, new PassFocusCommand());
            }
        }
    }
}
