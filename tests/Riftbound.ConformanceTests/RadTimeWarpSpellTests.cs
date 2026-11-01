using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTimeWarpSpellTests
{
    private const string Spell = "RAD·051/167";
    private const string ReturnUnit = "RETURN_FRIENDLY_UNIT:";

    [Fact]
    public async Task UsesTheExactOfficialPrintingAndSharedReturnUnitCostBehavior()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Spell);
        Assert.Equal(39547L, card.Id);
        Assert.Equal("时光交错", card.CardName);
        Assert.Equal(2, card.Energy);
        Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal("法术", card.CardCategoryName);
        Assert.Equal("让一名友方单位返回其所属的手牌，作为打出此牌的额外费用。\n对战场上的一名单位造成5点伤害。", card.CardEffect);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Spell, out var behavior));
        Assert.Equal(2, behavior.ManaCost);
        Assert.Equal(5, behavior.DamageAmount);
        Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.Equal(CardTargetScopes.BattlefieldUnit, behavior.TargetScope);
        Assert.True(behavior.RequiresReturnFriendlyUnitAdditionalCost);
        Assert.False(behavior.CanPlayDuringPriority);
        Assert.False(behavior.CanPlayDuringSpellDuel);
    }

    [Theory]
    [InlineData("ALLY", "GRAVE", "P1", "BATTLEFIELD")]
    [InlineData("ENEMY", "ALLY", "P2", "BATTLEFIELD")]
    public async Task ReturnsTheChosenFriendlyUnitBeforeResolvingFiveDamageToEitherBattlefieldUnit(
        string target, string payment, string targetController, string targetZone)
    {
        var initial = Position(target, payment, targetController, targetZone);
        var command = Play(target, payment);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());

        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Contains(payment, game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(payment, game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(payment, game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.State.StackItems);
        Assert.Equal(0, game.State.CardObjects[target].Damage);
        var returned = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Equal("ADDITIONAL_COST", returned.Event.Payload!["reason"]);
        Assert.Equal(payment, returned.Event.Payload["targetObjectId"]);

        await game.Top();
        Assert.Equal(5, game.State.CardObjects[target].Damage);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task AReturnedTargetFizzesWithoutRefundingTheCostOrRetargeting()
    {
        var initial = Position("ALLY", "ALLY", "P1", "BATTLEFIELD");
        var game = new Driver(initial);
        await game.Act("P1", Play("ALLY", "ALLY"));

        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        await game.Top();

        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        Assert.DoesNotContain("ALLY", game.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(IllegalPlays))]
    public async Task IllegalTargetPaymentTimingOrResourcesPreserveStateAndViews(
        string branch)
    {
        var state = Position("ENEMY", "ALLY", "P2", "BATTLEFIELD");
        var command = Play("ENEMY", "ALLY");
        switch (branch)
        {
            case "missing-payment": command = command with { OptionalCosts = [] }; break;
            case "equipment-payment": command = command with { OptionalCosts = [ReturnUnit + "GEAR"] }; break;
            case "enemy-payment": command = command with { OptionalCosts = [ReturnUnit + "ENEMY"] }; break;
            case "graveyard-payment": command = command with { OptionalCosts = [ReturnUnit + "GRAVE"] }; break;
            case "hidden-payment":
                state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    ["ALLY"] = state.CardObjects["ALLY"] with { IsFaceDown = true } } };
                break;
            case "duplicate-payment": command = command with { OptionalCosts = [ReturnUnit + "ALLY", ReturnUnit + "GRAVE"] }; break;
            case "base-target": command = command with { TargetObjectIds = ["ALLY"] }; break;
            case "equipment-target": command = command with { TargetObjectIds = ["GEAR"] }; break;
            case "battlefield-target": command = command with { TargetObjectIds = ["BF"] }; break;
            case "no-mana": state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 20) } }; break;
            case "no-power": state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0) } }; break;
            case "wrong-turn": state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" }; break;
        }

        await Reject(state, command);
    }

    public static IEnumerable<object[]> IllegalPlays() => new[] {
        "missing-payment", "equipment-payment", "enemy-payment", "graveyard-payment", "hidden-payment",
        "duplicate-payment", "base-target", "equipment-target", "battlefield-target", "no-mana", "no-power", "wrong-turn"
    }.Select(branch => new object[] { branch });

    [Fact]
    public async Task CounteringPreventsDamageButNeverRefundsTheReturnedUnit()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("ENEMY", "ALLY", "P2", "BATTLEFIELD"),
            "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", Play("ENEMY", "ALLY"));
        var spell = Assert.Single(game.State.StackItems);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId]));
        await game.Top();

        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > initial.CardObjects["ALLY"].ObjectGeneration);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    private static MatchState Position(string target, string payment, string targetController, string targetZone)
    {
        var state = VenUnitEntryBasicTests.Position(Spell);
        state = RadUnitEffectTestFixture.Place(state, target, targetController, targetZone, targetZone == "BATTLEFIELD" ? "OTHER" : null);
        if (targetController == "P1" && targetZone == "BATTLEFIELD")
            state = RadUnitEffectTestFixture.Place(state, target, "P1", "BATTLEFIELD", "BF");
        if (!string.Equals(payment, target, StringComparison.Ordinal))
            state = RadUnitEffectTestFixture.Place(state, payment, "P1", "BASE");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        cards[target] = cards[target] with { Power = 10 };
        return state with {
            CardObjects = cards,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) }
        };
    }

    private static PlayCardCommand Play(string target, string payment)
        => new("UNIT", Spell, [target], OptionalCosts: [ReturnUnit + payment]);

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-time-warp", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
