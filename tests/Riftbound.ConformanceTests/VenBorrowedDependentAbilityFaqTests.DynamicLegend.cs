using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("RAD·147/167")]
    [InlineData("RAD·172/167")]
    [InlineData("RAD·172*/167")]
    public async Task BorrowedSteadfastConversionUsesTheSharedDynamicKeywordAndFriendlyTarget(string printing)
    {
        var game = await WithTemplate(printing, "LEGEND");
        var id = TemplateId(game.State, "强攻等同于坚守");
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("borrowed-dynamic-enemy", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, ["ENEMY"]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        var mana = game.State.RunePools["P1"].Mana;
        var donor = game.State.CardObjects["DONOR"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(donor, game.State.CardObjects["DONOR"]);
        await game.Top();
        Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("COMBAT_KEYWORD:强攻等同于坚守:", StringComparison.Ordinal));
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·139/167", false)] [InlineData("RAD·139/167", true)]
    [InlineData("RAD·168/167", false)] [InlineData("RAD·168/167", true)]
    [InlineData("RAD·168*/167", false)] [InlineData("RAD·168*/167", true)]
    public async Task BorrowedNextUnitDiscountKeepsTheBorrowerAndTheIndependentEndInstruction(string printing, bool returnBorrower)
    {
        const string unit = "SFD·143/221";
        var game = await WithTemplate(printing, "LEGEND", empower: true,
            adjust: initial => RadGearTestPosition.Put(initial, "NEXT", unit, "P1", "HAND"));
        var id = TemplateId(game.State, "下一名单位费用减少");
        var donor = game.State.CardObjects["DONOR"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(donor, game.State.CardObjects["DONOR"]);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Top();
        var grant = Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.Equal("HOST", grant.Source.ObjectId);
        Assert.Equal(printing, grant.BorrowedGrant!.Borrowed!.Donor.CardNo);
        var original = game.State.UntilEndOfTurnEffects.Single(effect => effect.StartsWith(CoreRuleEngine.TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal));
        var forged = CoreRuleEngine.TimeAssassinNextUnitReductionMarker(grant.TurnNumber, grant.ControllerId,
            grant.Source, grant.CardNo, grant.CreatedTick, grant.BorrowedGrant with { CardNo = printing });
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(effect => effect == original ? forged : effect).ToArray() }));
        if (returnBorrower)
        {
            await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        var play = new PlayCardCommand("NEXT", unit, [], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var before = game.State.RunePools["P1"];
        await game.Act("P1", play);
        Assert.Equal(before.Mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        var privateMarker = Assert.Single(game.State.CardObjects["NEXT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        var publicJson = System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State),
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.DoesNotContain(privateMarker, publicJson);
        Assert.DoesNotContain("NEXT_UNIT_EPHEMERAL_DISCOUNT", publicJson);
        await game.ColdCheckpoint();
        await FinishBorrowedDiscountTurn(game);
    }

    [Theory]
    [InlineData("before-resolution")]
    [InlineData("after-grant")]
    [InlineData("after-play")]
    public async Task BorrowedNextUnitDiscountSurvivesATokenBorrowerCeasing(string departedAt)
    {
        const string unit = "SFD·143/221";
        var initial = RadGearTestPosition.Put(BorrowedBombPosition("RAD·139/167"), "NEXT", unit, "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = true,
                TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "下一名单位费用减少"), []));
        if (departedAt != "before-resolution") await game.Top();
        if (departedAt != "after-play") await ReturnBorrower();
        if (departedAt == "before-resolution") await game.Top();
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1",
            PlayCostPreviewTests.Request(game.State, new("NEXT", unit, [], Destination: "BASE")));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", new PlayCardCommand("NEXT", unit, [], Destination: "BASE"));
        if (departedAt == "after-play") await ReturnBorrower();
        Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        var marker = Assert.Single(game.State.CardObjects["NEXT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        var context = CoreRuleEngine.ReadTimeAssassinDestructionMarker(marker)!;
        var grant = context.BorrowedGrant!;
        var borrowed = grant.Borrowed!;
        Assert.NotNull(borrowed.Borrower.CeasedSource);
        foreach (var forged in new[] { borrowed with { Borrower = borrowed.Borrower with { CeasedSource = null } },
            borrowed with { Borrower = null! } })
        {
            var invalid = CoreRuleEngine.TimeAssassinDestructionMarker(context.TurnNumber, context.ControllerId,
                context.Source, context.Target, context.CardNo, context.CreatedTick, grant with { Borrowed = forged });
            Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                    ["NEXT"] = game.State.CardObjects["NEXT"] with { UntilEndOfTurnEffects = [invalid] } } }));
        }
        await game.ColdCheckpoint();
        await FinishBorrowedDiscountTurn(game);

        async Task ReturnBorrower()
        {
            if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
    }

    private static async Task FinishBorrowedDiscountTurn(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        for (var step = 0; game.State.TurnPlayerId == "P1"; step++)
        {
            Assert.InRange(step, 0, 16);
            if (ResolutionResult.HasOpenOrderTriggersWindow(game.State))
                await game.Act(ResolutionResult.OrderTriggersOrderingPlayerId(game.State)!,
                    new OrderTriggersCommand(OrderedTriggerIds: CoreRuleEngine.TriggerResolutionOrderIds(game.State)));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("End step has no next action.");
        }
        Assert.Contains("NEXT", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task BorrowedLegendSkillGrantedByAnotherCardSurvivesThatProvidersDeparture(bool returnProvider)
    {
        var game = await WithTemplate("VEN·149", "LEGEND", adjust: initial => {
            initial = RadGearTestPosition.Put(initial, "GRANT", "RAD·040/167", "P1", "BASE");
            return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["DONOR"] = initial.CardObjects["DONOR"] with { Tags = [CardObjectTags.LegendCard] } } };
        });
        var id = TemplateId(game.State, "战力");
        var power = CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        var borrowed = Assert.Single(game.State.StackItems).BorrowedExhaust!;
        Assert.Equal("GRANT", borrowed.BattlefieldSource!.ObjectId);
        if (returnProvider)
        {
            if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
            await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["GRANT"]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(power + 3, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["ALLY"]));
        await game.ColdCheckpoint(); await game.Replay();
    }
}
