using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Exact source facts and shared physical/resource-cost paths. These controlled
// cases do not authorize whole cards, decks or a canonical match birth.
public sealed class ReviewedScopedPrintUniverseTests
{
    private static readonly object[][] Sources = [
        [40107L, "RAD·038/167", 4, 4, "green", 1, "", true, 1],
        [40122L, "RAD·050/167", 5, 4, "blue", 0, "", false, 2],
        [40126L, "RAD·054/167", 3, 3, "blue", 0, "约德尔人", false, 1],
        [40127L, "RAD·055/167", 3, 2, "blue", 0, "机械", false, 0],
        [40136L, "RAD·063/167", 5, 5, "blue", 1, "", true, 1],
        [40138L, "RAD·065/167", 4, 3, "blue", 1, "", true, 2],
        [40139L, "RAD·065a/167", 4, 3, "blue", 1, "", true, 2],
        [40171L, "RAD·093/167", 4, 3, "purple", 0, "约德尔人", false, 2],
        [40175L, "RAD·097/167", 2, 1, "purple", 0, "", false, 1],
        [40187L, "RAD·109/167", 4, 4, "purple", 1, "恶魔", true, 1],
        [40188L, "RAD·109a/167", 4, 4, "purple", 1, "恶魔", true, 1],
        [40296L, "RAD·SP1/005", 4, 4, "green", 1, "", true, 1],
        [40298L, "RAD·SP3/005", 4, 4, "purple", 1, "恶魔", true, 1],
        // API raw Might4 remains metadata only; complete official face says5.
        [40299L, "RAD·SP4/005", 5, 4, "blue", 1, "", true, 1]
    ];

    public static IEnumerable<object[]> AllRows => Sources;
    public static IEnumerable<object[]> PhysicalRows => Sources.Where(row => (long)row[0] != 40299)
        .Select(row => new[] { row[0], row[1], row[2], row[3], row[4], row[6], row[7] });
    public static IEnumerable<object[]> ResetRows => PhysicalRows.SelectMany(row =>
        new[] { "HAND", "GRAVEYARD" }.Select(zone => row.Append(zone).ToArray()));
    public static IEnumerable<object[]> ResourceCostRows => Sources.Select(row => new[] { row[0], row[1], row[4], row[5] });
    public static IEnumerable<object[]> PlayRows => Sources.Select(row => new[] { row[0], row[1] });

    [Theory]
    [MemberData(nameof(AllRows))]
    public void EveryExactSourceKeepsItsOwnMetadataAndIndependentKeywordFacts(
        long id, string no, int mana, int might, string color, int power, string tag, bool hero, int disarm)
    {
        Assert.True(ScopedOfficialCardSources.TryGet(no, out var card));
        Assert.Equal(id, card.Id); Assert.Equal(no, card.CardNo);
        Assert.Equal(mana, card.Energy); Assert.Equal(might, card.Power);
        Assert.Equal(hero ? 1 : (int?)null, card.ReturnEnergy);
        Assert.Equal(power, Math.Max(0, card.ReturnEnergy ?? 0));
        Assert.Equal([color], card.CardColorList); Assert.Equal(tag, card.Tag);
        Assert.Equal(hero ? "英雄单位" : "单位", card.CardCategoryName);
        Assert.True(DisarmSourceSpecifications.TryGet(no, out var spec));
        Assert.Equal(id, spec.SourceId); Assert.Equal(no, spec.CardNo);
        Assert.Equal(disarm, spec.PrintedValue); Assert.Equal(card.CardEffect, spec.EffectiveText);
        if (id == 40127)
        {
            var grant = Assert.Single(spec.FriendlyGrants);
            Assert.Equal("机械", grant.RequiredUnitTag); Assert.Equal(1, grant.Value);
        }
        else Assert.Empty(spec.FriendlyGrants);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        Keep("metadata-" + id, new { card, spec });
    }

    [Theory]
    [MemberData(nameof(PhysicalRows))]
    public void EveryUnconflictedPrintCreatesItsOwnReviewedPhysicalIdentity(
        long id, string no, int mana, int might, string color, string tag, bool hero)
    {
        var accepted = Create(no, out var card);
        Keep("create-" + id, new { no, accepted, card });
        Assert.True(accepted, no);
        Printed(card, no, mana, might, color, tag, hero);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
    }

    [Theory]
    [MemberData(nameof(ResetRows))]
    public void SharedResetRetainsEachUnconflictedPrintAndClearsItsPreviousCharacteristics(
        long id, string no, int mana, int might, string color, string tag, bool hero, string zone)
    {
        var before = new CardObjectState("CARD", power: 99, manaCost: 99, cardNo: no,
            ownerId: "P1", controllerId: "P2", tags: [CardObjectTags.UnitCard, "COLOR:red", "TEMPORARY"])
            { ObjectGeneration = 9, Damage = 2, IsExhausted = true };
        var cards = new Dictionary<string, CardObjectState> { ["CARD"] = before };
        var zones = Zones(zone); Reset(zones, cards, before);
        Keep("reset-" + id + "-" + zone, new { no, zone, before, cards, zones });
        var actual = Assert.Contains("CARD", cards);
        Printed(actual, no, mana, might, color, tag, hero);
        Assert.Equal(10, actual.ObjectGeneration);
        Assert.DoesNotContain("TEMPORARY", actual.Tags);
        Assert.Contains("CARD", zone == "HAND" ? zones["P1"].Hand : zones["P1"].Graveyard);
    }

    [Theory]
    [MemberData(nameof(ResourceCostRows))]
    public void ReviewedPrintedResourceCostSelectsAndDebitsOnlyItsActualDomain(
        long id, string no, string color, int power)
    {
        var cost = PrintedPowerCostRules.ForCard(no);
        Keep("cost-definition-" + id, new { no, cost });
        Assert.Equal(power, cost.Amount); Assert.Equal([color], cost.Traits);
        var other = color == RuneTrait.Green ? RuneTrait.Blue : RuneTrait.Green;
        var pool = new RunePool(0, 0, new Dictionary<string, int> { [color] = 2, [other] = 2 });
        IReadOnlyDictionary<string, int> empty = new Dictionary<string, int>();
        var choice = power == 0 ? null : "PRINTED_POWER:" + color + ":1";
        Assert.True(PrintedPowerCostRules.TrySelect(no, choice, pool, 0, empty, out var generic, out var typed));
        Assert.Equal(0, generic); Assert.Equal(power, typed.Values.Sum());
        if (power > 0) Assert.Equal(power, typed[color]); else Assert.Empty(typed);
        Assert.True(PaymentCostRules.CanPayPowerCost(pool, generic, typed));
        var paid = PaymentCostRules.PayPowerCost(pool, generic, typed);
        Assert.Equal(2 - power, paid.PowerByTrait[color]); Assert.Equal(2, paid.PowerByTrait[other]);
        var wrongPool = new RunePool(0, 0, new Dictionary<string, int> { [other] = 2 });
        Assert.Equal(power == 0, PaymentCostRules.CanPayPowerCost(wrongPool, generic, typed));
        if (power > 0)
            Assert.False(PrintedPowerCostRules.TrySelect(no, "PRINTED_POWER:" + other + ":1", pool, 0, empty, out _, out _));
        Keep("cost-debit-" + id, new { no, cost, choice, pool, generic, typed, paid, wrongPool });
    }

    [Theory]
    [MemberData(nameof(PlayRows))]
    public async Task SourceAndCostKnowledgeCannotAuthorizeAnIncompleteWholeCard(long id, string no)
    {
        // SP4 has no admitted physical form; the unknown placeholder exercises
        // the unsupported-play boundary without selecting either disputed Might.
        if (!Create(no, out var card)) card = new("CARD", cardNo: no, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
        var initial = new MatchState("REVIEWED-SOURCE-NOT-PLAYABLE", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(100, 100), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = card },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["CARD"] = new("P1", "HAND") });
        var hash = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("not-enabled-" + id, "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", no, []), default);
        Keep("unsupported-" + id, new { initial, result });
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(initial)); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
    }

    [Fact]
    public void ConflictedSP4KeepsItsOwnRawSourceAndKeywordWithoutAcquiringEitherPhysicalMight()
    {
        Assert.True(ScopedOfficialCardSources.TryGet("RAD·SP4/005", out var raw));
        Assert.Equal(40299, raw.Id); Assert.Equal(4, raw.Power); // Retained API value, not a physical ruling.
        Assert.True(DisarmSourceSpecifications.TryGet("RAD·SP4/005", out var spec));
        Assert.Equal(40299, spec.SourceId); Assert.Equal(1, spec.PrintedValue);
        Assert.False(Create("RAD·SP4/005", out _));
        Assert.True(Create("RAD·063/167", out var regular)); Assert.Equal(5, regular.Power);
        Assert.Equal("RAD·063/167", regular.CardNo);
        Keep("conflicted-sp4", new { raw, spec, regular });
    }

    [Fact]
    public void NamesAliasesAndUnobservedPrintsRemainUnknownToAllThreeLookups()
    {
        foreach (var no in new[] { "RAD·038", "RAD·038a/167", "RAD·065b/167", "RAD·SP4", "卡莎", "RAD·063/167 ", " RAD·063/167" })
        {
            Assert.False(ScopedOfficialCardSources.TryGet(no, out _)); Assert.False(DisarmSourceSpecifications.TryGet(no, out _));
            Assert.False(Create(no, out _)); Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
            Assert.Empty(PrintedPowerCostRules.ForCard(no).Traits);
        }
    }

    [Theory]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    public void AnImageStillCeasesEvenWhenItsFaceHasUnresolvedPhysicalSourceData(string zone)
    {
        var before = new CardObjectState("CARD", cardNo: "RAD·SP4/005", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard])
            { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo };
        var cards = new Dictionary<string, CardObjectState> { ["CARD"] = before }; var zones = Zones(zone);
        Reset(zones, cards, before);
        Assert.DoesNotContain("CARD", cards.Keys); Assert.DoesNotContain("CARD", zones["P1"].Hand.Concat(zones["P1"].Graveyard));
        Keep("sp4-image-" + zone, new { before, cards, zones });
    }

    private static void Printed(CardObjectState actual, string no, int mana, int might, string color, string tag, bool hero)
    {
        Assert.Equal("CARD", actual.ObjectId); Assert.Equal(no, actual.CardNo);
        Assert.Equal("P1", actual.OwnerId); Assert.Equal("P1", actual.ControllerId);
        Assert.Equal(mana, actual.ManaCost); Assert.Equal(might, actual.Power);
        Assert.Equal(0, actual.Damage); Assert.False(actual.IsExhausted); Assert.Null(actual.TokenFactoryCardNo);
        Assert.Contains(CardObjectTags.UnitCard, actual.Tags); Assert.Contains("COLOR:" + color, actual.Tags);
        Assert.Equal(hero, actual.Tags.Contains("CARD_TYPE:HERO", StringComparer.Ordinal));
        if (tag.Length > 0) Assert.Contains(tag, actual.Tags);
    }

    private static bool Create(string no, out CardObjectState card)
    {
        object?[] args = ["CARD", "P1", no, null];
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        var result = (bool)factory.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        card = (CardObjectState)args[3]!; return result;
    }

    private static Dictionary<string, PlayerZones> Zones(string zone)
        => new() { ["P1"] = PlayerZones.Empty with { Hand = zone == "HAND" ? ["CARD"] : [],
            Graveyard = zone == "GRAVEYARD" ? ["CARD"] : [] }, ["P2"] = PlayerZones.Empty };

    private static void Reset(Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, CardObjectState before)
        => typeof(CoreRuleEngine).GetMethod("ResetCardOutsidePlay", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [zones, cards, "CARD", before, "P1"]);

    private static void Keep(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_REVIEWED_UNIVERSE_EVIDENCE") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
