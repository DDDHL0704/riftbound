using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedUnitTokenPosition(string donor)
    {
        var state = LocalDestructionRecallTests.At(BorrowedConditionPosition(donor, donor == "VEN·087"),
            "HOST", "P1", "BATTLEFIELD", "F");
        if (state.CardObjects["DONOR"].Tags.Contains("CARD_TYPE:LEGEND"))
            state = state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with {
                        Base = state.PlayerZones["P1"].Base.Where(id => id != "DONOR").ToArray(), LegendZone = ["DONOR"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DONOR"] = new("P1", "LEGEND") } };
        return HandDiscardObserverTests.Put(state, "ARMAMENT", "SFD·022/221", "P1", "HAND");
    }

    private static CardObjectState[] BorrowedCreatedUnits(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null && c.Tags.Contains(CardObjectTags.UnitCard))
            .OrderBy(c => c.ObjectId, StringComparer.Ordinal).ToArray();

    private static async Task FinishBorrowedTokenLocations(VenBasicFlowSpellTests.Driver game, bool alternate = false)
    {
        for (var i = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } location; i++)
        {
            Assert.True(i < 8);
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:MISSING"]));
            await game.Act("P1", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", [alternate && i % 2 == 1 ? "BATTLEFIELD:F" : "BASE"]));
        }
        Assert.Null(game.State.PendingRuleChoice);
    }

    [Theory]
    [InlineData("OGN·265/298", "随从", 1, 1, false)]
    [InlineData("OGN·308/298", "随从", 1, 1, false)]
    [InlineData("OGN·308*/298", "随从", 1, 1, false)]
    [InlineData("FND-265/298", "随从", 1, 1, false)]
    [InlineData("SFD·168/221", "随从", 3, 1, false)]
    [InlineData("UNL-160/219", "战鹰", 2, 1, false)]
    [InlineData("VEN·087", "机器人", 1, 3, false)]
    [InlineData("SFD·197/221", "黄沙士兵", 1, 2, true)]
    [InlineData("SFD·247/221", "黄沙士兵", 1, 2, true)]
    public async Task BorrowedUnitTokenSkillsPayOnlyTheBorrowersCostsAndKeepPrintedRestrictions(
        string donor, string family, int count, int might, bool armamentRequired)
    {
        var initial = BorrowedUnitTokenPosition(donor);
        var game = new VenBasicFlowSpellTests.Driver(initial);
        if (armamentRequired)
        {
            await game.Act("P1", new PlayCardCommand("ARMAMENT", "SFD·022/221", ["ALLY"], Destination: "BASE"));
            await game.Top();
        }
        var id = TemplateId(game.State, $"战力的{family}");
        var before = game.State;
        await RejectBorrowedResource(before, "HOST", id, ["ALLY"]);
        if (armamentRequired) await RejectBorrowedResource(initial, "HOST", id, []);
        if (family == "战鹰")
            await RejectBorrowedResource(LocalDestructionRecallTests.At(before, "HOST", "P1", "BASE"), "HOST", id, []);
        if (donor == "VEN·087")
            await RejectBorrowedResource(before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
                ["HOST"] = before.CardObjects["HOST"] with { IsEmpowered = false } } }, "HOST", id, []);
        if (family != "战鹰" && count == 1)
            await RejectBorrowedResource(before with { RunePools = new Dictionary<string, RunePool>(before.RunePools) { ["P1"] = new(0, 20) } }, "HOST", id, []);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Empty(BorrowedCreatedUnits(game.State));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(before.CardObjects["DONOR"], game.State.CardObjects["DONOR"]);
        Assert.Equal(before.RunePools["P1"].Mana - (family != "战鹰" && count == 1 ? 1 : 0), game.State.RunePools["P1"].Mana);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        if (donor == "VEN·087") Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        if (armamentRequired)
        {
            var item = Assert.Single(game.State.StackItems);
            Assert.True(item.BorrowedExhaust!.ArmamentWasPlayedThisTurn);
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust with { ArmamentWasPlayedThisTurn = false } }] }));
        }
        await game.Top();
        if (family is "机器人" or "黄沙士兵") Assert.Null(game.State.PendingRuleChoice);
        await FinishBorrowedTokenLocations(game);
        var tokens = BorrowedCreatedUnits(game.State);
        Assert.Equal(count, tokens.Length);
        Assert.All(tokens, token => {
            Assert.Equal(might, token.Power);
            Assert.True(token.IsExhausted);
            Assert.Equal("P1", token.OwnerId);
            Assert.Equal("P1", token.ControllerId);
            Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(token.TokenFactoryCardNo!, out var factory));
            Assert.Equal(family, factory.TokenFamilyName);
            if (family == "战鹰") Assert.Contains(CardObjectTags.Spellshield, token.Tags);
            if (family == "黄沙士兵") Assert.Contains(CardEquipmentKeywordNames.Tempered, token.Tags);
        });
        Assert.Equal(count, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        if (armamentRequired)
        {
            Assert.NotNull(Assert.Single(game.State.StackItems).Tempered);
            var attach = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("TEMPERED_CONFIRMATION", attach.ChoiceWindow);
            await game.Act("P1", new ChooseCardsCommand(attach.ChoiceId, attach.ChoiceWindow, []));
        }
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·265/298", "source")]
    [InlineData("UNL-160/219", "source")]
    [InlineData("UNL-160/219", "donor")]
    [InlineData("VEN·087", "source")]
    public async Task BorrowedUnitTokenInstructionSurvivesRealSourceAndDonorRecall(string donor, string response)
    {
        var game = new VenBasicFlowSpellTests.Driver(BorrowedUnitTokenPosition(donor));
        var family = donor == "VEN·087" ? "机器人" : donor == "UNL-160/219" ? "战鹰" : "随从";
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, $"战力的{family}"), []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [response == "source" ? "HOST" : "DONOR"]));
        await game.Top();
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        Assert.Equal(family == "战鹰" ? 2 : 1, BorrowedCreatedUnits(game.State).Length);
        Assert.Equal("HAND", game.State.ObjectLocations[response == "source" ? "HOST" : "DONOR"].Zone);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·168/221", 3)]
    [InlineData("UNL-160/219", 2)]
    public async Task BorrowedUnitTokensChooseSeparateLegalPlayLocations(string donor, int count)
    {
        var game = new VenBasicFlowSpellTests.Driver(BorrowedUnitTokenPosition(donor));
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "战力的"), []));
        await game.Top();
        Assert.Equal("TOKEN_PLAY_LOCATION", game.State.PendingRuleChoice!.Request.Kind);
        await FinishBorrowedTokenLocations(game, alternate: true);
        var tokens = BorrowedCreatedUnits(game.State);
        Assert.Equal(count, tokens.Length);
        Assert.Equal(1, tokens.Count(t => game.State.ObjectLocations[t.ObjectId].BattlefieldObjectId == "F"));
        Assert.Equal(count - 1, tokens.Count(t => game.State.ObjectLocations[t.ObjectId].Zone == "BASE"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedUnitTokensUseTheSharedReplacementBeforeAnyEntryOrLocationChoice(bool accept)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(BorrowedUnitTokenPosition("SFD·168/221"), "DOUBLER", "UNL-086/219", "F", "P1");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "战力的随从"), []));
        await game.Top();
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Empty(BorrowedCreatedUnits(game.State));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { TokenEntryPlan = item.TokenEntryPlan! with { OriginalCount = 99 } }] }));
        var first = true;
        while (game.State.PendingRuleChoice is null
            && game.State.PendingCardChoice is { ChoiceWindow: "TOKEN_ENTRY_REPLACEMENT" } choice)
        {
            await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DOUBLER"]));
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept && first ? ["DOUBLER"] : []));
            first = false;
        }
        await FinishBorrowedTokenLocations(game);
        Assert.Equal(accept ? 4 : 3, BorrowedCreatedUnits(game.State).Length);
        Assert.Equal(accept, game.State.CardObjects["DOUBLER"].UntilEndOfTurnEffects.Contains("TOKEN_ENTRY_REPLACEMENT_USED"));
        await game.Replay();
    }
}
