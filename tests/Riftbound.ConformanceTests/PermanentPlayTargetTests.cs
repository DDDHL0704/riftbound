using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PermanentPlayTargetTests
{
    [Fact]
    public async Task MissingAnEnemyDoesNotTrapTheEnteredUnitInAnImpossiblePairChoice()
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position("SFD·132/221"), "FRIEND", "OGN·096/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "FRIEND", "P1", "BASE");
        var result = await Act(state, "P1", new PlayCardCommand("UNIT", "SFD·132/221", []));
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("orphan")]
    [InlineData("response")]
    [InlineData("binding")]
    [InlineData("flag")]
    public async Task CurrentRecoveryRejectsForgedPermanentTargetWindows(string mutation)
    {
        var played = await Act(Position("VEN·017"), "P1", new PlayCardCommand("UNIT", "VEN·017", []));
        var item = Assert.Single(played.State.StackItems);
        var forged = played.State with { StackItems = [mutation switch {
            "source" => item with { SourceConfirmed = false },
            "binding" => item with { TargetGenerations = new Dictionary<string, long> { ["UNIT"] = 0 } },
            "flag" => item with { PermanentPlayTargetsPending = false },
            _ => item
        }] };
        if (mutation == "orphan") forged = forged with { PendingCardChoice = null };
        if (mutation == "response") forged = forged with { PriorityPlayerId = "P1" };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Theory]
    [InlineData("UNL-021/219")]
    [InlineData("SFD·007/221")]
    [InlineData("SFD·032/221")]
    [InlineData("SFD·039/221")]
    [InlineData("SFD·053/221")]
    [InlineData("SFD·061/221")]
    [InlineData("SFD·062/221")]
    [InlineData("SFD·101/221")]
    [InlineData("SFD·132/221")]
    [InlineData("SFD·138/221")]
    [InlineData("SFD·158/221")]
    [InlineData("OGS·010/024")]
    [InlineData("OGN·016/298")]
    [InlineData("OGN·051/298")]
    [InlineData("OGN·063/298")]
    [InlineData("OGN·082/298")]
    [InlineData("OGN·092/298")]
    [InlineData("OGN·097/298")]
    [InlineData("OGN·132/298")]
    [InlineData("OGN·136/298")]
    [InlineData("OGN·141/298")]
    [InlineData("OGN·149/298")]
    [InlineData("OGN·165/298")]
    [InlineData("OGN·188/298")]
    [InlineData("OGN·191/298")]
    [InlineData("OGN·199/298")]
    [InlineData("OGN·225/298")]
    [InlineData("OGN·226/298")]
    [InlineData("OGN·234/298")]
    [InlineData("VEN·017")]
    [InlineData("VEN·186")]
    public async Task EachListedPublicTargetPermanentCanEnterBeforeAnyAbilityTarget(string cardNo)
    {
        var state = Position(cardNo);
        var result = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, []));
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal(1, result.State.CardObjects["UNIT"].ObjectGeneration);
        if (result.State.PendingCardChoice is { } choice) {
            Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
            Assert.Null(result.State.PriorityPlayerId);
        }
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    internal static MatchState Position(string cardNo)
    {
        var state = ArmamentMightTests.Position(unit: "OGN·001/298", power: 5);
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = ["UNIT", "P1", cardNo, null];
        Assert.True((bool)factory.GetMethod("TryCreate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, args)!);
        var card = Assert.IsType<CardObjectState>(args[3]);
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = card },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "HAND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(30, 30), ["P2"] = new(30, 30) }
        };
    }

    [Theory]
    [InlineData("VEN·017", true)]
    [InlineData("VEN·186", true)]
    [InlineData("VEN·017", false)]
    [InlineData("VEN·186", false)]
    public async Task WardIsAnIndependentAbilityCostAndDeclineDoesNotUndoThePermanent(string cardNo, bool pay)
    {
        var state = LocalDestructionRecallTests.At(VendettaAmbushTests.Position(cardNo), "HOST", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { OwnerId = "P2", ControllerId = "P2",
                Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", new("quote", "prompt", state.Tick,
            new PlayCardCommand("UNIT", cardNo, [])));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(5, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, []));
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        Assert.Equal(19, played.State.RunePools["P1"].TotalPower);
        var selected = await OfficialHeldConfirmationTests.Choose(played.State, "HOST");
        Assert.Equal(1, selected.State.PendingPayment!.PowerCost);
        Assert.Equal(19, selected.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, selected.State.CardObjects["HOST"].Damage);
        Restore(selected.State); ArmamentMightTests.PublicRecovery(selected.State);
        var paid = await OfficialHeldConfirmationTests.Pay(selected.State, pay);
        Assert.Contains("UNIT", paid.State.PlayerZones["P1"].Base);
        Assert.Equal(pay ? 18 : 19, paid.State.RunePools["P1"].TotalPower);
        if (pay) paid = await Top(paid.State);
        Assert.Equal(pay ? 4 : 2, paid.State.CardObjects["HOST"].Damage);
        Assert.Empty(paid.State.StackItems);
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
    }

    [Theory]
    [InlineData("OGN·132/298")]
    [InlineData("OGN·141/298")]
    [InlineData("SFD·132/221")]
    public async Task AnotherOrOtherTargetsExcludeTheEnteredSource(string cardNo)
    {
        var state = VendettaAmbushTests.Position(cardNo, 0);
        state = OfficialHoldSequenceTests.AddUnit(state, "FRIEND", "OGN·096/298", "F", "P1");
        state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY", "OGN·096/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "FRIEND", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BASE");
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, []));
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.DoesNotContain("UNIT", choice.LegalObjectIds);
        Assert.Contains("FRIEND", choice.LegalObjectIds);
        Restore(played.State);
    }

    [Theory]
    [InlineData("VEN·017")]
    [InlineData("VEN·186")]
    public async Task PermanentEntersBeforeItsPlayAbilityChoosesTheNewUnit(string cardNo)
    {
        var played = await Act(VendettaAmbushTests.Position(cardNo), "P1", new PlayCardCommand("UNIT", cardNo, []));
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Contains("UNIT", choice.LegalObjectIds);
        Assert.Null(played.State.PriorityPlayerId);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
        var chosen = await OfficialHeldConfirmationTests.Choose(played.State, "UNIT");
        Assert.Null(chosen.State.PendingCardChoice);
        var item = Assert.Single(chosen.State.StackItems);
        Assert.True(item.SourceConfirmed);
        Assert.Equal(chosen.State.CardObjects["UNIT"].ObjectGeneration, item.TargetGenerations!["UNIT"]);
        Restore(chosen.State);
        var resolved = await Top(chosen.State);
        Assert.Equal(0, resolved.State.CardObjects["UNIT"].Damage);
        Assert.Empty(resolved.State.StackItems);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }
}
