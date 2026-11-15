using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadElnukHerdErrataTests
{
    private const string Herd = "RAD·081/167";

    [Fact]
    public async Task RuntimeUsesTheChineseErratumWithoutChangingTheAcquiredFace()
    {
        var acquired = Assert.Single(VendettaCardSources.GetOfficialCards(), card => card.Id == 39580 && card.CardNo == Herd);
        var admitted = Assert.Single(RadiantCardSources.GetAdmittedCards(), card => card.Id == 39580 && card.CardNo == Herd);
        var runtime = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.Id == 39580 && card.CardNo == Herd);
        Assert.Contains("给予另一名友方单位", acquired.CardEffect, StringComparison.Ordinal);
        Assert.Equal(acquired.CardEffect.Replace("给予另一名友方单位", "给予一名友方单位", StringComparison.Ordinal), admitted.CardEffect);
        Assert.Equal(admitted.CardEffect, runtime.CardEffect);
        Assert.Equal(acquired.Energy, runtime.Energy);
        Assert.Equal(acquired.ReturnEnergy, runtime.ReturnEnergy);
        Assert.Equal(acquired.Power, runtime.Power);
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task AFaceUpHerdCanChooseItselfAndItsMightExpiresAfterColdRecoveryAndRawReplay(string destination)
    {
        var initial = VenUnitEntryBasicTests.Position(Herd);
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", Herd, [], Destination: destination));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("UNIT", choice.LegalObjectIds);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds);
        await game.ColdCheckpoint();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        Assert.Equal(generation, Assert.Single(game.State.StackItems).TargetGenerations!["UNIT"]);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(8, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        await game.ColdCheckpoint();
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AStandbyHerdCanChooseItselfAndRecyclingDoesNotBuffItsNewGeneration()
    {
        var initial = StandbySourceRulesTests.Position(Herd);
        initial = AttackDamageDistributionTests.Spell(initial, "REPLY", "OGN·048/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SHADOW"] = OfficialGarenConquestBattlefieldCountTests.Printed("SHADOW", Herd, "P1"),
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", initial.CardObjects["HOST"].CardNo!, "P1") } };
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new HideCardCommand("SHADOW", Herd, "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", ["SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2"]);
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("SHADOW", Herd, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("SHADOW", choice.LegalObjectIds);
        Assert.Contains("HOST", choice.LegalObjectIds);
        await game.ColdCheckpoint();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHADOW"]));
        var generation = game.State.CardObjects["SHADOW"].ObjectGeneration;
        var skill = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SHADOW");
        Assert.NotNull(skill.PlayedFromStandby);
        Assert.Equal(generation, skill.TargetGenerations!["SHADOW"]);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal("MAIN_DECK", game.State.ObjectLocations["SHADOW"].Zone);
        Assert.Equal(generation + 1, game.State.CardObjects["SHADOW"].ObjectGeneration);
        Assert.Equal(5, game.State.CardObjects["SHADOW"].Power);
        Assert.Equal(0, game.State.CardObjects["SHADOW"].UntilEndOfTurnPowerModifier);
        Assert.Empty(game.State.CardObjects["SHADOW"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(initial.CardObjects["HOST"].Power, game.State.CardObjects["HOST"].Power);
        var modified = Assert.Single(game.Events(), entry => entry.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "SHADOW"));
        Assert.Equal(3, modified.Event.Payload["powerDelta"]);
        Assert.Equal(8, modified.Event.Payload["resultingPower"]);
        var recycled = Assert.Single(game.Events(), entry => entry.Event.Kind == "CARDS_RECYCLED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "SHADOW"));
        Assert.True(modified.Sequence < recycled.Sequence);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(5, game.State.CardObjects["SHADOW"].Power);
        await game.ColdCheckpoint();
    }
}
