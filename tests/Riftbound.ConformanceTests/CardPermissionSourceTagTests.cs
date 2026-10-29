using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CardPermissionSourceTagTests
{
    [Theory]
    [InlineData("", "", "反应", false)]
    [InlineData("|| \t|", "　|\u00a0", "反应", false)]
    [InlineData("反应", "", "反应", true)]
    [InlineData("", "反应", "反应", true)]
    [InlineData("急速|反应", "武器|反应", "反应", true)]
    [InlineData("　急速\u00a0|\t反应　|", "", "\u00a0反应\t", true)]
    [InlineData("", "|\u2003反应\u202f||急速|", "　反应　", true)]
    [InlineData("反应性|超反应", "", "反应", false)]
    [InlineData("反应", "", "反", false)]
    [InlineData("反应", "", "反应|急速", false)]
    [InlineData("SWIFT", "", "swift", false)]
    [InlineData("SWIFT", "", "SWIFT", true)]
    [InlineData("é", "", "e\u0301", false)]
    [InlineData("|反应|", "", "|", false)]
    [InlineData("反应", "急速", "", false)]
    [InlineData("反应", "急速", "　\t\u00a0", false)]
    public void LooksUpWholeOrdinalTagsFromEitherSource(
        string unitTags, string equipmentTags, string keyword, bool expected)
    {
        Assert.Equal(expected, CardPermissionKeywordRules.HasSourceKeyword(Definition(unitTags, equipmentTags), keyword));
    }

    [Fact]
    public void NullQueriesAreAbsentAndNullBehaviorsAreRejected()
    {
        Assert.False(CardPermissionKeywordRules.HasSourceKeyword(Definition("反应", "急速"), null!));
        Assert.Throws<ArgumentNullException>(() => CardPermissionKeywordRules.HasSourceKeyword(null!, "反应"));
    }

    [Fact]
    public void EveryRegisteredSourceMatchesTheExistingDelimitedTagSemantics()
    {
        var definitions = CardBehaviorRegistry.GetAll().ToArray();
        Assert.NotEmpty(definitions);
        var keywords = definitions
            .SelectMany(b => (b.SourceUnitTags + "|" + b.SourceEquipmentTags)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(tag => new[] { tag, "\t" + tag + "　", tag + "missing", "prefix" + tag })
            .Concat(["", "　", "missing", "|"])
            .ToArray();
        foreach (var definition in definitions)
        {
            foreach (var keyword in keywords)
            {
                Assert.Equal(ReferenceLookup(definition, keyword),
                    CardPermissionKeywordRules.HasSourceKeyword(definition, keyword));
            }
        }
    }

    [Fact]
    public void DelimiterAndUnicodeWhitespaceCombinationsMatchTheExistingSemantics()
    {
        string[] segments = ["", " ", "\t　", "反应", " 急速　", "武器", "SWIFT", "swift", "é", "e\u0301"];
        string[] keywords = ["", "　", "反应", " 急速　", "武器", "SWIFT", "swift", "é", "e\u0301", "反", "|"];
        var random = new Random(424242);
        for (var example = 0; example < 256; example++)
        {
            string Tags() => string.Join('|', Enumerable.Range(0, random.Next(0, 12))
                .Select(_ => segments[random.Next(segments.Length)]));
            var definition = Definition(Tags(), Tags());
            foreach (var keyword in keywords)
            {
                Assert.Equal(ReferenceLookup(definition, keyword),
                    CardPermissionKeywordRules.HasSourceKeyword(definition, keyword));
            }
        }
    }

    [Fact]
    public void RepeatedSuccessfulAndMissingLookupsDoNotAllocateTagCollections()
    {
        var definition = Definition("||　急速\t|反应||", "|武器|迅捷|");
        var found = 0;
        for (var warmup = 0; warmup < 100; warmup++) Lookup();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var repeat = 0; repeat < 1_000; repeat++) Lookup();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(2_200, found);
        Assert.Equal(0, allocated);

        void Lookup()
        {
            if (CardPermissionKeywordRules.HasSourceKeyword(definition, "　反应\t")) found++;
            if (CardPermissionKeywordRules.HasSourceKeyword(definition, "迅捷")) found++;
            if (CardPermissionKeywordRules.HasSourceKeyword(definition, "missing")) found++;
        }
    }

    private static CardBehaviorDefinition Definition(string unitTags, string equipmentTags)
        => new("TEST-TAGS", "source tags", 0, "NONE", 0, 0,
            SourceUnitTags: unitTags, SourceEquipmentTags: equipmentTags);

    private static bool ReferenceLookup(CardBehaviorDefinition behavior, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        return Parse(behavior.SourceUnitTags).Concat(Parse(behavior.SourceEquipmentTags))
            .Distinct(StringComparer.Ordinal)
            .Any(tag => string.Equals(tag, keyword.Trim(), StringComparison.Ordinal));

        static IEnumerable<string> Parse(string value)
            => value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part));
    }
}
