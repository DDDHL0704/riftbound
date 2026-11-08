using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    // Images really have Ephemeral. Earn HOLD through the actual assembled
    // HOLD/CONQUEST alias during this turn rather than skipping their death step.
    [Theory, InlineData(false), InlineData(true)]
    public async Task AnActualAssembledAliasLetsAnImageEarnTheHeldGrantAndThenCease(bool beforeCreation)
    {
        var initial = RadGearTestPosition.Create() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        foreach (var (id, no, owner, zone) in new[] {
            ("TEMPLATE", Nami, "P2", "BASE"), ("COPY", "UNL-200/219", "P1", "HAND"),
            ("ALIAS", "SFD·030/221", "P1", "BASE"), ("FIRST", Unit, "P1", "HAND"),
            ("RETURN", "OGN·104/298", "P1", "HAND") })
            initial = RadGearTestPosition.Put(initial, id, no, owner, zone);
        var palette = new[] { "UNL-R03", "UNL-R03", "UNL-R03", "UNL-R03", "UNL-R01", "UNL-R01",
            "UNL-R02", "UNL-R02", "UNL-R04", "UNL-R04", "UNL-R06", "UNL-R06" };
        for (var i = 0; i < palette.Length; i++)
            initial = RadGearTestPosition.Put(initial, "COPY-FUNDS-" + i, palette[i], "P1", "BASE");
        var game = new Driver(initial);
        await game.TapRunes("P1", Enumerable.Range(0, palette.Length).Select(i => "COPY-FUNDS-" + i));
        await game.Act("P1", new RecycleRuneCommand("COPY-FUNDS-0", ["COPY-FUNDS-0", "COPY-FUNDS-1"]));
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"],
            OptionalCosts: [PrintedPowerCostRules.ChoiceId(new Dictionary<string, int> { [RuneTrait.Blue] = 2 })]));
        while (game.State.StackItems.Count > 0) await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Nami, image.CardNo);
        Assert.Contains(CardObjectTags.Ephemeral, image.Tags);
        await game.Act("P1", new RecycleRuneCommand("COPY-FUNDS-4"));
        await game.Act("P1", new AssembleEquipmentCommand("ALIAS", image.ObjectId, ["ASSEMBLE_RED"]));
        await game.Top();
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var held = Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Nami);
        Assert.Equal("HOLD", held.UnitEvent!.Timing);
        Assert.Equal("CONQUEST", held.TimingAlias!.EffectiveTiming);
        if (!beforeCreation) await game.Top();
        await game.Act("P1", new RecycleRuneCommand("COPY-FUNDS-2"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        Assert.Contains(game.Events(), record => record.Event.Kind == "TOKEN_CEASED_TO_EXIST"
            && Equals(record.Event.Payload.GetValueOrDefault("targetObjectId"), image.ObjectId));
        if (beforeCreation) await game.Top();
        await game.ColdCheckpoint();
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(image.ObjectId, Assert.Single(game.State.StackItems).TurnUnitPlayBoon!.Grant.Source.ObjectId);
        await game.ColdCheckpoint(); await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        await game.ColdCheckpoint();
    }
}
