using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulSacrificeCostTests
{
    internal const string Card = "UNL-173/219";
    internal const string Cost = "DESTROY_FRIENDLY_POWERFUL_UNIT:HOST";
    internal static PlayCardCommand Command() => new("SPELL", Card, [], OptionalCosts: [Cost]);
    internal static MatchState Equipped() => PowerfulConsumerMightTests.Hand(
        ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2, attached: true), Card);
    internal static async Task<MatchState> Suppressed()
    {
        var state = PowerfulConsumerMightTests.Suppressed();
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BASE");
        state = (await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []))).State;
        return PowerfulConsumerMightTests.Hand(state, Card);
    }

    [Fact]
    public async Task MightyAdditionalCostAcceptsPrintedTwoEquippedToCurrentFive()
    {
        var initial = Equipped();
        Assert.Equal(2, initial.CardObjects["HOST"].Power);
        Assert.Equal(5, ArmamentMightTests.Might(initial));
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, Command()));
        Assert.True(quote.CanPay, quote.Message);
        var paid = await Act(initial, "P1", Command());
        Assert.Contains("HOST", paid.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);
        Assert.Contains(paid.Events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("reason"), "ADDITIONAL_COST"));
        Assert.Equal(19, paid.State.RunePools["P1"].Mana);
        Assert.Equal(2, paid.State.StackItems.Count);
        Assert.Equal(TriggerKinds.UnitLastBreathPowerfulDraw, paid.State.StackItems[^1].EffectKind);
        Assert.Equal(5, paid.State.StackItems[^1].LastBreathMight!.Might);
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
        var breath = await Top(paid.State);
        Assert.Equal(2, breath.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(breath.State.StackItems); Assert.Equal(3, breath.State.PlayerZones["P1"].RuneDeck.Count);
        Restore(breath.State); ArmamentMightTests.PublicRecovery(breath.State);
        var spell = await Top(breath.State);
        Assert.Equal(4, spell.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(2, spell.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.True(spell.State.CardObjects["R1"].IsExhausted);
        Assert.Empty(spell.State.StackItems);
        Restore(spell.State); ArmamentMightTests.PublicRecovery(spell.State);
    }

    [Fact]
    public async Task MightyAdditionalCostRejectsStoredEightSuppressedToCurrentTwo()
    {
        var initial = await Suppressed();
        Assert.Equal(8, initial.CardObjects["HOST"].Power);
        Assert.Equal(2, ArmamentMightTests.Might(initial));
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, Command()));
        Assert.False(quote.CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("weak-cost", "P1", CommandTypes.PlayCard), Command(), default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events);
    }

    internal static JsonElement? Requirement(MatchState state)
    {
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)["P1"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var candidate in prompt.GetProperty("candidates").EnumerateArray().Where(c => c.GetProperty("action").GetString() == "PLAY_CARD"))
            foreach (var requirement in candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray())
                if (requirement.GetProperty("sourceObjectId").GetString() == "SPELL") return requirement;
        return null;
    }

    [Fact]
    public void ThePromptOffersTheMandatoryCostAsAComposableNonTargetChoice()
    {
        var state = Equipped(); var requirement = Requirement(state)!.Value;
        Assert.True(requirement.GetProperty("composable").GetBoolean());
        Assert.Equal(0, requirement.GetProperty("minTargetCount").GetInt32());
        var choices = requirement.GetProperty("optionalCostChoices").EnumerateArray()
            .Where(c => c.GetProperty("id").GetString()!.StartsWith("DESTROY_FRIENDLY_POWERFUL_UNIT:")).ToArray();
        Assert.Equal(Cost, Assert.Single(choices).GetProperty("id").GetString());
        Assert.Contains("必付", choices[0].GetProperty("reason").GetString());
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state,
            new("SPELL", Card, []))).CanPay);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, true)]
    public async Task CostPromptPreviewAndPaymentAgreeAtTheCurrentMightBoundary(int stored, bool allowed)
    {
        var state = Equipped(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = stored } } };
        Assert.Equal(stored + 3, ArmamentMightTests.Might(state));
        Assert.Equal(allowed, Requirement(state).HasValue);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Command()));
        Assert.Equal(allowed, quote.CanPay);
        var paid = await new CoreRuleEngine().ResolveAsync(state, new("boundary", "P1", CommandTypes.PlayCard), Command(), default);
        Assert.Equal(allowed, paid.Accepted);
        if (!allowed) { Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(paid.State)); Assert.Empty(paid.Events); }
    }

    [Theory]
    [InlineData("enemy")] [InlineData("face-down")] [InlineData("standby")]
    [InlineData("equipment")] [InlineData("hand")]
    public async Task AnInvalidCostObjectIsNeitherOfferedNorAccepted(string change)
    {
        var state = Equipped(); var card = state.CardObjects["HOST"];
        card = change switch {
            "enemy" => card with { ControllerId = "P2" },
            "face-down" => card with { IsFaceDown = true },
            "standby" => card with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] },
            "equipment" => card with { Tags = [CardObjectTags.EquipmentCard], Power = 8 },
            _ => card
        };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = card } };
        if (change == "hand") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(), Hand = ["SPELL", "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        Assert.Null(Requirement(state));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Command())).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid", "P1", CommandTypes.PlayCard), Command(), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task AnOldPreviewDoesNotAuthorizeSacrificeAfterTheArmamentDetaches()
    {
        var state = Equipped(); var engine = new CoreRuleEngine();
        Assert.True(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Command())).CanPay);
        state = PowerfulConsumerMightTests.Hand(state, "SFD·011/221", "DETACH");
        var detached = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State)).State;
        Assert.Equal(2, ArmamentMightTests.Might(detached)); Assert.Null(Requirement(detached));
        Assert.False(engine.PreviewPlayCard(detached, "P1", PlayCostPreviewTests.Request(detached, Command())).CanPay);
        var rejected = await engine.ResolveAsync(detached, new("after-detach", "P1", CommandTypes.PlayCard), Command(), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(detached), MatchStateHasher.Hash(rejected.State));
        Restore(detached); ArmamentMightTests.PublicRecovery(detached);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task GrantedEchoComposesWithOneMightyCostWithoutRepeatingTheSacrifice(int echoes)
    {
        var state = Equipped() with { UntilEndOfTurnEffects = Enumerable.Range(0, echoes)
            .Select(i => EchoCostRules.GrantPrefix + "P1" + (i == 0 ? "" : ":" + i)).ToArray() };
        var costs = new[] { Cost }.Concat(Enumerable.Range(0, echoes).Select(i => i == 0 ? "ECHO" : "ECHO:GRANTED:" + (i + 1))).ToArray();
        var offered = Requirement(state)!.Value.GetProperty("optionalCostChoices").EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ToArray();
        Assert.All(costs, cost => Assert.Contains(cost, offered));
        var command = new PlayCardCommand("SPELL", Card, [], OptionalCosts: costs);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1 + echoes, quote.Cost!.Mana);
        var paid = await Act(state, "P1", command);
        Assert.Single(paid.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(1 + echoes, paid.State.StackItems[0].EffectRepeatCount);
        var breath = await Top(paid.State); Assert.Equal(2, breath.State.PlayerZones["P1"].Hand.Count);
        var spell = await Top(breath.State);
        Assert.Equal(2 + 2 * (1 + echoes), spell.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(3 - (1 + echoes), spell.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Single(spell.State.PlayerZones["P1"].Graveyard, id => id == "HOST");
        Restore(spell.State); ArmamentMightTests.PublicRecovery(spell.State);
    }
}
