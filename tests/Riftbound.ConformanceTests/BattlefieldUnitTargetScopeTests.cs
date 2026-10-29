using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldUnitTargetScopeTests
{
    private static readonly string[] Cards = ["VEN·146", "VEN·059", "VEN·010", "UNL-007/219"];

    public static IEnumerable<object[]> InvalidTargets() => Cards.SelectMany(card =>
        new[] { "battlefield", "equipment", "rune", "spell", "hidden", "base" }
            .Select(kind => new object[] { card, kind }));

    [Theory]
    [MemberData(nameof(InvalidTargets))]
    public async Task QuotesPromptsAndSubmissionRejectObjectsThatAreNotVisibleBattlefieldUnits(string card, string kind)
    {
        var initial = VenSignatureFlowSpellTests.Position(card);
        var target = kind == "battlefield" ? "FIELD" : "ENEMY";
        if (kind == "base") initial = LocalDestructionRecallTests.At(initial, target, "P2", "BASE");
        if (kind is "equipment" or "rune" or "spell" or "hidden")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [target] = initial.CardObjects[target] with {
                    IsFaceDown = kind == "hidden",
                    Tags = [kind switch { "equipment" => CardObjectTags.EquipmentCard,
                        "rune" => CardObjectTags.RuneCard, "spell" => CardObjectTags.SpellCard,
                        _ => CardObjectTags.UnitCard }] } } };
        if (kind == "equipment")
        {
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [target] = initial.CardObjects[target] with { CardNo = "SFD·022/221", AttachedToObjectId = "GEAR-HOST",
                    Tags = [CardObjectTags.EquipmentCard, "武装", "灵便"] },
                ["GEAR-HOST"] = new("GEAR-HOST", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2",
                    power: 5, tags: [CardObjectTags.UnitCard]) } };
            initial = LocalDestructionRecallTests.At(initial, "GEAR-HOST", "P2", "BATTLEFIELD", "OTHER");
        }
        var command = new PlayCardCommand("SPELL", card, [target]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.IsValid); Assert.False(quote.CanPay);
        var choices = Choices(initial);
        Assert.DoesNotContain(target, choices);
        Assert.Contains("FRIEND", choices);
        await VenSignatureFlowSpellTests.Reject(initial, command, ErrorCodes.InvalidTarget);
    }

    public static IEnumerable<object[]> ValidTargets() => Cards.SelectMany(card =>
        new[] { false, true }.Select(equipment => new object[] { card, equipment }));

    [Theory]
    [MemberData(nameof(ValidTargets))]
    public async Task VisibleUnitsRemainLegalWhenTheyAlsoHaveTheEquipmentType(string card, bool equipment)
    {
        var initial = VenSignatureFlowSpellTests.Position(card);
        if (equipment) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.EquipmentCard] } } };
        var command = new PlayCardCommand("SPELL", card, ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        Assert.Contains("ENEMY", Choices(initial));
        var played = await OfficialGraveyardRecastTests.Act(initial, "P1", command);
        var resolved = await OfficialGraveyardRecastTests.Top(played.State);
        Assert.Contains(resolved.Events, ev => ev.Kind == "DAMAGE_APPLIED"
            && Equals(ev.Payload["targetObjectId"], "ENEMY"));
    }

    private static IReadOnlyList<string> Choices(MatchState state)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,
            candidate => candidate.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]);
        var spell = Assert.Single(requirements, requirement => Equals(requirement["sourceObjectId"], "SPELL"));
        var byIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(spell["targetChoicesByIndex"]);
        return Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(byIndex["0"])
            .Select(choice => choice.Id).ToArray();
    }
}
