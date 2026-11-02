using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DisarmCopiedSourceContinuityTests
{
    [Fact]
    public async Task RecoveryRejectsACopiedGrantWhoseSourceNeverExisted()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(RadDisarmUnitTests.Board("RAD·055/167"),
            "COPY", "UNL-200/219");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["HOST"]));
        await game.Top();
        await game.Top(); // The copied unit enters through its own pending play.
        await CloseEmptyDuel(game);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var item = Assert.Single(game.State.StackItems);
        var origin = item.FieldContext!.Disarm!;
        var copied = Assert.Single(origin.Contributions, contribution => contribution.Source.ObjectId == "COPY-TOKEN-001");
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State));

        var forged = origin with { Contributions = origin.Contributions.Select(contribution => contribution == copied
            ? contribution with { Source = new("NEVER-ENTERED-TOKEN", copied.Source.Generation) } : contribution).ToArray() };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
            StackItems = [item with { FieldContext = item.FieldContext with { Disarm = forged } }] }));
        await game.ColdCheckpoint();
    }

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
        await game.Top(); // The copied unit enters through its own pending play.
        await CloseEmptyDuel(game);
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
            var captured = remaining.FieldContext!.Disarm!;
            var copied = Assert.Single(captured.Contributions, contribution => contribution.Source.ObjectId == tokenId);
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(copied.CeasedSource);
            foreach (var invalidReceipt in new CeasedSelfAbilitySource?[] {
                null,
                receipt with { Source = new("NEVER-ENTERED-TOKEN", receipt.Source.Generation) },
                receipt with { DepartedTick = game.State.Tick + 1 }
            })
            {
                var forged = captured with { Contributions = captured.Contributions.Select(contribution => contribution == copied
                    ? contribution with { CeasedSource = invalidReceipt } : contribution).ToArray() };
                Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
                    StackItems = [remaining with { FieldContext = remaining.FieldContext! with { Disarm = forged } }] }));
            }
            // Both receipts are in the past, but they still describe the same
            // captured grant and must not let recovery count it twice.
            Assert.True(receipt.DepartedTick > captured.CapturedTick + 1);
            var repeated = copied with { CeasedSource = receipt with { DepartedTick = captured.CapturedTick + 1 } };
            var duplicated = captured with { Contributions = [..captured.Contributions, repeated] };
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
                StackItems = [remaining with { FieldContext = remaining.FieldContext! with { Disarm = duplicated } }] }));
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
            await CloseEmptyDuel(game);
        }
    }

    private static async Task CloseEmptyDuel(Driver game)
    {
        for (var passes = 0; game.State.StackItems.Count == 0 && game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.True(passes < 4, "An empty spell duel must close after both players pass focus.");
            await game.Act(player, new PassFocusCommand());
        }
    }
}
