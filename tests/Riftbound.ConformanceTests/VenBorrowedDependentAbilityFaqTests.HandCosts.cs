using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string PalaceHandCostAbility = "CRUMBLING_PALACE_DISCARD_CREATE_WARHAWK";

    // Official CN UNL-088/219 (34624); VEN FAQ p. 15 preserves all
    // inherited activation costs, paid with the borrowing player's cards.
    private static MatchState PalaceHandCostPosition(bool borrowed, string costCard = "OGN·048/298")
    {
        var state = BorrowedBombPosition("UNL-088/219");
        state = HandDiscardObserverTests.Put(state, "PALACE-COST", costCard, "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "ENEMY-PRIVATE-COST", "OGN·048/298", "P2", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = borrowed } } };
    }

    private static string PalaceSkill(MatchState state, bool borrowed)
        => borrowed ? TemplateId(state, "战力的战鹰") : PalaceHandCostAbility;

    private static IEnumerable<IReadOnlyDictionary<string, object?>> PalaceRequirements(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!);

    [Theory]
    [InlineData(false, "OGN·048/298", "BASE")]
    [InlineData(true, "OGN·048/298", "BASE")]
    [InlineData(false, "OGN·001/298", "BASE")]
    [InlineData(true, "OGN·001/298", "BASE")]
    [InlineData(false, "SFD·022/221", "BATTLEFIELD:BF")]
    [InlineData(true, "SFD·022/221", "BATTLEFIELD:BF")]
    public async Task CrumblingPalaceDiscardsAnyHandCardBeforeCreatingItsSpellshieldWarhawk(
        bool borrowed, string costCard, string destination)
    {
        var initial = PalaceHandCostPosition(borrowed, costCard);
        initial = HandDiscardObserverTests.Put(initial, "RAMPAGE", "OGN·019/298", "P1", "HAND");
        if (destination != "BASE") initial = OfficialHoldSequenceTests.AddUnit(initial,
            "PALACE-FIELD-ALLY", "OGN·219/298", "BF", "P1");
        var source = borrowed ? "HOST" : "UNIT";
        var ability = PalaceSkill(initial, borrowed);
        var row = Assert.Single(PalaceRequirements(initial), r => Equals(r.GetValueOrDefault("sourceObjectId"), source)
            && Equals(r.GetValueOrDefault("abilityId"), ability));
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        var costs = (IReadOnlyList<ActionPromptChoiceDto>)row["additionalCostChoices"]!;
        Assert.Contains(costs, c => c.Id == "DISCARD_HAND_CARD:PALACE-COST");
        Assert.DoesNotContain(costs, c => c.Id.Contains("ENEMY-PRIVATE-COST", StringComparison.Ordinal));
        Assert.Contains("法盾", (string)row["abilityLabel"]!);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(source, ability, [], ["DISCARD_HAND_CARD:PALACE-COST"]));
        Assert.Contains("PALACE-COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("PALACE-COST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["PALACE-COST"].Zone);
        Assert.Contains("DISCARDED_HAND_CARD_THIS_TURN:P1", game.State.UntilEndOfTurnEffects);
        Assert.Equal(initial.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects[source].IsExhausted);
        if (borrowed) Assert.Equal(initial.CardObjects["UNIT"], game.State.CardObjects["UNIT"]);
        Assert.Empty(BorrowedCreatedUnits(game.State));
        var kinds = game.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(kinds, "CARD_DISCARDED") < Array.IndexOf(kinds, "STACK_ITEM_ADDED"));
        await game.Top();
        if (game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } location)
        {
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", [destination]));
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:MISSING"]));
            await game.Act("P1", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", [destination]));
        }
        var token = Assert.Single(BorrowedCreatedUnits(game.State));
        Assert.Equal(P6TokenFactoryCatalog.WarhawkTokenCardNo, token.TokenFactoryCardNo);
        Assert.Equal(1, token.Power);
        Assert.True(token.IsExhausted);
        Assert.Contains(CardObjectTags.Spellshield, token.Tags);
        Assert.Equal("P1", token.OwnerId);
        Assert.Equal("P1", token.ControllerId);
        Assert.Equal(destination == "BASE" ? new("P1", "BASE") : new ObjectLocationState("P1", "BATTLEFIELD", "BF"),
            game.State.ObjectLocations[token.ObjectId]);
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("RAMPAGE", "OGN·019/298", [], Destination: "BASE"));
        Assert.Contains(CardCombatKeywordNames.Assault, game.State.CardObjects["RAMPAGE"].Tags);
        Assert.Contains(CardCombatKeywordNames.Roam, game.State.CardObjects["RAMPAGE"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrumblingPalaceRejectsInvalidHandCostsAndTargetsWithoutChangingState(bool borrowed)
    {
        var state = PalaceHandCostPosition(borrowed);
        var source = borrowed ? "HOST" : "UNIT";
        var ability = PalaceSkill(state, borrowed);
        foreach (var costs in new[] { Array.Empty<string>(), new[] { "DISCARD_HAND_CARD:ENEMY-PRIVATE-COST" },
            ["DISCARD_HAND_CARD:ALLY"], ["DISCARD_HAND_CARD:PALACE-COST", "DISCARD_HAND_CARD:RETURN"],
            ["DISCARD_HAND_CARD:MISSING"] })
            await RadGearTestPosition.Reject(state, "P1", new ActivateAbilityCommand(source, ability, [], costs));
        await RadGearTestPosition.Reject(state, "P1", new ActivateAbilityCommand(source, ability, ["ALLY"], ["DISCARD_HAND_CARD:PALACE-COST"]));
        await RadGearTestPosition.Reject(state, "P2", new ActivateAbilityCommand(source, ability, [], ["DISCARD_HAND_CARD:PALACE-COST"]));
        var hand = state.PlayerZones["P1"].Hand;
        var empty = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Hand = [], Graveyard = [..state.PlayerZones["P1"].Graveyard, ..hand] } },
            ObjectLocations = state.ObjectLocations.ToDictionary(p => p.Key,
                p => hand.Contains(p.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : p.Value) };
        Assert.DoesNotContain(PalaceRequirements(empty), r => Equals(r.GetValueOrDefault("sourceObjectId"), source)
            && Equals(r.GetValueOrDefault("abilityId"), ability));
        await RadGearTestPosition.Reject(empty, "P1", new ActivateAbilityCommand(source, ability, [], ["DISCARD_HAND_CARD:PALACE-COST"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrumblingPalacePaidDiscardQueuesTheRealJinxObserverBeforeTheWarhawk(bool borrowed)
    {
        var initial = HandDiscardObserverTests.Put(PalaceHandCostPosition(borrowed), "JINX", "OGN·202/298", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["JINX"] = initial.CardObjects["JINX"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", PalaceSkill(initial, borrowed), [],
            ["DISCARD_HAND_CARD:PALACE-COST"]));
        Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null);
        Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["JINX"].IsExhausted);
        Assert.Equal(initial.CardObjects["JINX"].Power + 1, game.State.CardObjects["JINX"].Power);
        Assert.Empty(BorrowedCreatedUnits(game.State));
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        Assert.Single(BorrowedCreatedUnits(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrumblingPalaceBorrowedSkillKeepsItsPaidCostWhenTheSourceReturnsOrCeases(bool tokenSource)
    {
        var initial = PalaceHandCostPosition(true);
        if (tokenSource) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", PalaceSkill(initial, true), [], ["DISCARD_HAND_CARD:PALACE-COST"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("PALACE-COST", game.State.PlayerZones["P1"].Graveyard);
        if (tokenSource) Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        Assert.Single(BorrowedCreatedUnits(game.State));
        var created = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal(tokenSource ? "HOST" : "HIDDEN", created.Event.Payload["sourceObjectId"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrumblingPalaceUsesSharedTokenReplacementBeforeLocationSelection(bool borrowed)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(PalaceHandCostPosition(borrowed), "DOUBLER", "UNL-086/219", "BF", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", PalaceSkill(initial, borrowed), [],
            ["DISCARD_HAND_CARD:PALACE-COST"]));
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
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, first ? ["DOUBLER"] : []));
            first = false;
        }
        await FinishBorrowedTokenLocations(game);
        Assert.Equal(2, BorrowedCreatedUnits(game.State).Length);
        Assert.All(BorrowedCreatedUnits(game.State), c => Assert.Contains(CardObjectTags.Spellshield, c.Tags));
        await game.Replay();
    }
}
