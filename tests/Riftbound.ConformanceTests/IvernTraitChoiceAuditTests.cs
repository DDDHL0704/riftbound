using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IvernTraitChoiceAuditTests
{
    public static IEnumerable<object[]> Choices() =>
        from no in new[] { "UNL-177/219", "UNL-177a/219" }
        from choice in new[] { ("CHOOSE_BIRD", "鸟类"), ("CHOOSE_CAT", "猫科"), ("CHOOSE_DOG", "犬形"), ("CHOOSE_PORO", "魄罗") }
        select new object[] { no, choice.Item1, choice.Item2 };

    // CN UNL judge FAQ 260511 p12 prints "As you play me"; the tag is
    // selected during the play, independently of the later score trigger.
    [Theory, MemberData(nameof(Choices))]
    public async Task EachPrintingChoosesExactlyOneOfTheFourPrintedTagsDuringPlay(string no, string mode, string tag)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Mode: mode));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(initial.RunePools["P1"].Mana - 6, game.State.RunePools["P1"].Mana);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(new[] { tag }, game.State.CardObjects["UNIT"].Tags
            .Where(value => new[] { "鸟类", "猫科", "犬形", "魄罗" }.Contains(value)).ToArray());
        await game.Replay();
    }
}
