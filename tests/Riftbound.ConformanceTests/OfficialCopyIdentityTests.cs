using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialCopyIdentityTests
{
    [Theory]
    [InlineData("SFD·068/221", 3, 5)]
    [InlineData("OGN·096/298", 1, 2)]
    [InlineData("UNL-090/219", 4, 4)]
    public async Task CopyUsesPrintedCharacteristicsInsteadOfModifiedObject(string cardNo, int power, int cost)
    {
        var result = await Copy(Position(cardNo));
        var token = result.State.CardObjects["C-TOKEN-001"];
        Assert.Equal(power, token.Power); Assert.Equal(cost, token.ManaCost);
        Assert.Equal(cardNo, token.CardNo); Assert.Equal("P1", token.OwnerId); Assert.Equal("P1", token.ControllerId);
        Assert.False(token.IsExhausted); Assert.False(token.IsAttacking); Assert.Equal(0, token.Damage);
        Assert.Equal(0, token.UntilEndOfTurnPowerModifier); Assert.Empty(token.UntilEndOfTurnEffects);
        Assert.Null(token.AttachedToObjectId); Assert.DoesNotContain("游走", token.Tags);
        Assert.DoesNotContain("增益", token.Tags); Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        if (cardNo == "UNL-090/219") Assert.Contains("后排", token.Tags);
        Restore(result.State);
    }

    [Fact]
    public async Task CopyOfCopyKeepsTheCopiedFaceButNotItsLaterModifications()
    {
        var first = await Copy(Position("OGN·096/298"));
        var state = AddSpell(first.State, "SECOND", "UNL-200/219");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["C-TOKEN-001"] = state.CardObjects["C-TOKEN-001"] with { Power = 12, Tags = [CardObjectTags.UnitCard, "游走"] } } };
        var second = await Copy(state, "C-TOKEN-001", "SECOND");
        var token = second.State.CardObjects["SECOND-TOKEN-001"];
        Assert.Equal("OGN·096/298", token.CardNo); Assert.Equal(1, token.Power); Assert.Equal(2, token.ManaCost);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, token.TokenFactoryCardNo);
        Assert.DoesNotContain("游走", token.Tags); Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        Restore(second.State);
    }

    [Theory]
    [InlineData("OGN·273/298", 1, CardObjectTags.MinionTokenFamily)]
    [InlineData("UNL·T02", 1, "鸟类")]
    [InlineData("UNL·T07", 3, "仙灵")]
    public async Task CopyOfOrdinaryTokenCopiesFactoryCharacteristics(string card, int power, string tag)
    {
        var result = await Copy(Position(card)); var token = result.State.CardObjects["C-TOKEN-001"];
        Assert.Equal(card, token.CardNo); Assert.Equal(power, token.Power); Assert.Equal(0, token.ManaCost);
        Assert.Contains(tag, token.Tags); Assert.DoesNotContain("游走", token.Tags); Restore(result.State);
    }

    [Theory]
    [InlineData("OGN·229/298", true)] // destruction
    [InlineData("OGN·104/298", false)] // return to hand
    public async Task TokenDepartureUsesIntrinsicIdentityEvenWhenTagsChange(string spell, bool dies)
    {
        var first = await Copy(Position("OGN·096/298"));
        var state = AddSpell(first.State, "LEAVE", spell);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["C-TOKEN-001"] = state.CardObjects["C-TOKEN-001"] with { Tags = [CardObjectTags.UnitCard] } } };
        var cast = await Act(state, "P1", new PlayCardCommand("LEAVE", spell, ["C-TOKEN-001"]));
        var departed = await Top(cast.State);
        Assert.False(departed.State.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.False(departed.State.ObjectLocations.ContainsKey("C-TOKEN-001"));
        foreach (var zones in departed.State.PlayerZones.Values)
            Assert.DoesNotContain("C-TOKEN-001", zones.Hand.Concat(zones.MainDeck).Concat(zones.Graveyard).Concat(zones.Banished));
        Assert.Single(departed.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
        Assert.Equal(dies ? 1 : 0, departed.State.StackItems.Count);
        Assert.True(OfficialInsightAndSpellLockTests.Errors(departed.State).Count == 0,
            string.Join("; ", OfficialInsightAndSpellLockTests.Errors(departed.State)));
        Restore(departed.State);
        if (dies)
        {
            var count = departed.State.PlayerZones["P1"].Hand.Count;
            var resolved = await Top(departed.State);
            Assert.Equal(count + 1, resolved.State.PlayerZones["P1"].Hand.Count);
            Assert.Empty(resolved.State.StackItems); Restore(resolved.State);
        }
    }

    [Theory]
    [InlineData("SFD·155/221", "EQUIPMENT_TOKEN_CREATED")]
    [InlineData("UNL-153/219", "UNIT_TOKEN_CREATED")]
    public async Task CopiedDeathAbilitiesKeepTheirSourceFaceAfterTokenDisappears(string card, string eventKind)
    {
        var copied = await Copy(Position(card));
        var cast = await Act(AddSpell(copied.State, "KILL", "OGN·229/298"), "P1",
            new PlayCardCommand("KILL", "OGN·229/298", ["C-TOKEN-001"]));
        var died = await Top(cast.State); Assert.False(died.State.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.Equal(card, died.State.StackItems.Single().CardNo);
        Assert.DoesNotContain(died.Events, e => e.Kind == eventKind); Restore(died.State);
        var done = await Top(died.State);
        Assert.Single(done.Events, e => e.Kind == eventKind); Assert.Single(done.State.PlayerZones["P1"].Base);
        var spawned = done.State.CardObjects[done.State.PlayerZones["P1"].Base.Single()];
        Assert.True(TokenObjectRules.IsToken(spawned));
        if (card == "SFD·155/221") Assert.True(spawned.IsExhausted);
        else Assert.Contains(CardObjectTags.Spellshield, spawned.Tags);
        Restore(done.State);
        var forged = died.State with { StackItems = [died.State.StackItems.Single() with { CardNo = "SFD·068/221" }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Fact]
    public async Task CopiedTokenReceivesSoulShepherdAuraButTheOriginalCardDoesNot()
    {
        var state = Position();
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SHEPHERD"] = new("SHEPHERD", cardNo: "UNL-077/219", ownerId: "P1", controllerId: "P1", power: 4, tags: [CardObjectTags.UnitCard]),
                ["TARGET"] = state.CardObjects["TARGET"] with { ControllerId = "P1" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["SHEPHERD", "TARGET"] },
                ["P2"] = state.PlayerZones["P2"] with { Base = [] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["SHEPHERD"] = new("P1", "BASE"), ["TARGET"] = new("P1", "BASE") } };
        var result = await Copy(state);
        var aura = Assert.Single(result.State.ContinuousEffects, e => e.SourceObjectId == "SHEPHERD");
        Assert.Equal("C-TOKEN-001", aura.TargetObjectId); Assert.Equal(1, aura.PowerDelta);
        Restore(result.State);
    }

    [Fact]
    public async Task CopiedTokenPaysRecyclingCostWithoutEnteringDeckOrTriggeringLastBreath()
    {
        var copied = await Copy(Position("OGN·096/298"));
        var state = OfficialMechanicalRecastTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["B"] = copied.State.CardObjects["C-TOKEN-001"] with { ObjectId = "B", Power = 6 } } };
        var opened = await OfficialMechanicalRecastTests.Open(state); var choice = opened.State.PendingCardChoice!;
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["B"]));
        Assert.False(paid.State.CardObjects.ContainsKey("B")); Assert.DoesNotContain("B", paid.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(6, paid.State.PendingEffectPlay!.ManaReduction); Assert.Empty(paid.State.StackItems);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_DESTROYED"); Restore(paid.State);
    }

    [Fact]
    public async Task RecoveryRejectsInvalidFactoryAndTokenPersistingInHand()
    {
        var result = await Copy(Position()); var state = result.State;
        var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["C-TOKEN-001"] = state.CardObjects["C-TOKEN-001"] with { TokenFactoryCardNo = "OGN·096/298" } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("invalid token factory"));
        var inHand = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = [], Hand = ["C-TOKEN-001"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["C-TOKEN-001"] = new("P1", "HAND") } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(inHand), e => e.Contains("token cannot persist"));
    }

    internal static MatchState AddSpell(MatchState state, string id, string card) => state with {
        RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = new(id, cardNo: card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Append(id).ToArray() } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") } };

    internal static MatchState Position(string cardNo = "SFD·068/221")
    {
        var s = OfficialSpellCompletionTests.Position("UNL-200/219", "OGS·021/024", false);
        return s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["TARGET"] = new("TARGET", cardNo: cardNo, ownerId: "P2", controllerId: "P2", power: 13,
                    damage: 1, isExhausted: true, untilEndOfTurnPowerModifier: 4,
                    untilEndOfTurnEffects: ["STUNNED"], tags: [CardObjectTags.UnitCard, "游走", "增益"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = ["TARGET"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["TARGET"] = new("P2", "BASE") } };
    }
    internal static async Task<ResolutionResult> Copy(MatchState state, string target = "TARGET", string source = "C")
    {
        var cast = await Act(state, "P1", new PlayCardCommand(source, "UNL-200/219", [target]));
        var opened = await Top(cast.State);
        return await Top(opened.State);
    }
}
