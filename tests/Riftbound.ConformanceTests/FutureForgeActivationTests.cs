using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class FutureForgeActivationTests
{
    private const string Forge = "OGN·212/298";
    private static string Ability => P4ActivatedAbilityCatalog.GetAll().Single(ability =>
        ability.SourceCardNo == Forge && ability.Kind == ActivatedAbilityKinds.DestroySourceRecycleGraveyard).AbilityId;
    private static MatchState Position(bool hand = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "FORGE", Forge, "P1", hand ? "HAND" : "BASE");
        for (var i = 1; i <= 5; i++)
            state = RadGearTestPosition.Put(state, "G" + i, "OGN·219/298", i % 2 == 0 ? "P2" : "P1", "GRAVEYARD");
        return RadGearTestPosition.Put(state, "PRIVATE-HAND", "OGN·219/298", "P2", "HAND");
    }
    private static ActivateAbilityCommand Activate(params string[] ids) => new("FORGE", Ability, ids);
    private static IReadOnlyDictionary<string, object?> Requirement(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(candidate => candidate.Enabled && candidate.Action == CommandTypes.ActivateAbility
                && candidate.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(candidate => (IEnumerable<IReadOnlyDictionary<string, object?>>)candidate.Metadata!["sourceRequirements"]!)
            .Single(row => Equals(row.GetValueOrDefault("sourceObjectId"), "FORGE")
                && Equals(row.GetValueOrDefault("abilityId"), Ability));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task DestroyingTheEquipmentPaysNowAndRecyclesZeroToFourChosenCardsIntoTheirOwnersDecksLater(int count)
    {
        var game = new Driver(Position());
        var requirement = Requirement(game.State);
        Assert.Equal(0, requirement["minTargetCount"]); Assert.Equal(4, requirement["maxTargetCount"]);
        var choices = JsonSerializer.Serialize(requirement["targetChoicesByIndex"]);
        Assert.Contains("G1", choices); Assert.Contains("G2", choices);
        Assert.DoesNotContain("FORGE", choices); Assert.DoesNotContain("PRIVATE-HAND", choices);
        var selected = Enumerable.Range(1, count).Select(i => "G" + i).ToArray();
        await game.Act("P1", Activate(selected));
        Assert.Contains("FORGE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(30, game.State.RunePools["P1"].Mana); Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARDS_RECYCLED");
        Assert.Equal(selected, Assert.Single(game.State.StackItems).TargetObjectIds);
        foreach (var id in selected) Assert.Contains(id, game.State.PlayerZones.Values.SelectMany(zones => zones.Graveyard));
        await game.Top();
        foreach (var id in selected) {
            var owner = game.State.CardObjects[id].OwnerId!;
            Assert.Contains(id, game.State.PlayerZones[owner].MainDeck);
            Assert.DoesNotContain(id, game.State.PlayerZones.Values.SelectMany(zones => zones.Graveyard));
        }
        Assert.Contains("FORGE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(count, game.Events().Where(entry => entry.Event.Kind == "CARDS_RECYCLED")
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["count"])));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("hand")] [InlineData("deck")] [InlineData("field")]
    [InlineData("duplicate")] [InlineData("five")] [InlineData("wrong-player")]
    public async Task TargetsAreChosenBeforeDestructionAndInvalidChoicesCannotPayAnyCost(string kind)
    {
        var ids = kind switch {
            "source" => new[] { "FORGE" }, "hand" => ["PRIVATE-HAND"], "deck" => ["P2-D0"],
            "field" => ["ALLY"], "duplicate" => ["G1", "G1"], "five" => ["G1", "G2", "G3", "G4", "G5"], _ => ["G1"] };
        await RadGearTestPosition.Reject(Position(), kind == "wrong-player" ? "P2" : "P1", Activate(ids));
    }

    [Fact]
    public async Task ThePrintedDestructionCostDoesNotAlsoRequireAnActiveSourceOrExhaustIt()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FORGE"] = state.CardObjects["FORGE"] with { IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P1", Activate("G1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_EXHAUSTED");
        await game.Top(); Assert.Contains("G1", game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Fact]
    public async Task ARealResponseCanRecycleThePaidSourceAndOneChosenTargetWithoutRetargetingTheEarnedAbility()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RESPONSE", "UNL-103/219", "P2", "HAND"));
        await game.Act("P1", Activate("G1", "G2"));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "UNL-103/219", ["FORGE", "G1"], Mode: "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top();
        Assert.Contains("FORGE", game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("G1", game.State.PlayerZones["P1"].MainDeck);
        var prefix = game.Events().Length;
        await game.Top();
        Assert.All(game.Events().Skip(prefix).Where(entry => entry.Event.Kind is "ABILITY_RESOLVED" or "CARDS_RECYCLED"),
            entry => Assert.DoesNotContain(JsonSerializer.Serialize("FORGE"), JsonSerializer.Serialize(entry.Event.Payload)));
        Assert.Contains("G2", game.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(3, game.Events().Where(entry => entry.Event.Kind == "CARDS_RECYCLED")
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["count"])));
        Assert.DoesNotContain("PRIVATE-HAND", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Replay();
    }

    [Fact]
    public async Task TheCurrentControllerCanPayWithEquipmentOwnedByTheOpponent()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FORGE"] = state.CardObjects["FORGE"] with { OwnerId = "P2" } } };
        var game = new Driver(state); await game.Act("P1", Activate("G1"));
        Assert.Contains("FORGE", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); Assert.Contains("G1", game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Fact]
    public async Task PlayingTheCardRetainsItsIndependentOnePowerMinionAndDoesNotAutomaticallySacrificeTheEquipment()
    {
        var game = new Driver(Position(hand: true)); await game.Act("P1", new PlayCardCommand("FORGE", Forge, []));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("FORGE", game.State.PlayerZones["P1"].Base);
        var minion = Assert.Single(game.State.PlayerZones["P1"].Base.Select(id => game.State.CardObjects[id]),
            card => TokenObjectRules.IsToken(card) && card.Tags.Contains(CardObjectTags.UnitCard));
        Assert.Equal(1, minion.Power);
        await game.Act("P1", Activate("G1")); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("source-generation")] [InlineData("over-limit")] [InlineData("missing-target-generation")]
    public async Task RecoveryRejectsForgedActivationBindingsAndTargetBounds(string kind)
    {
        var game = new Driver(Position()); await game.Act("P1", Activate("G1"));
        var item = Assert.Single(game.State.StackItems);
        var changed = kind switch {
            "source-generation" => item with { SimpleActivation = item.SimpleActivation! with { Source = new("FORGE", 999) } },
            "over-limit" => item with { TargetObjectIds = ["G1", "G2", "G3", "G4", "G5"],
                TargetGenerations = Enumerable.Range(1, 5).ToDictionary(i => "G" + i, _ => 0L) },
            _ => item with { TargetGenerations = null } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [changed] }));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnEquipmentAnimatedAndMovedToABattlefieldRetainsItsSelfDestructionSkill()
    {
        var initial = RadGearTestPosition.Put(Position(), "LEGEND", "RAD·145/167", "P1", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        var game = new Driver(OriannaEquipmentConsumerFixture.WithResources(initial));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "FORGE");
        await game.Act("P1", new MoveUnitCommand("FORGE", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["FORGE"].IsExhausted);
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        Assert.Equal(Ability, Requirement(game.State)["abilityId"]);
        await game.Act("P1", Activate("G1"));
        Assert.Contains("FORGE", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); Assert.Contains("G1", game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Fact]
    public void TheImplicitActivatedCostAndItsTargetLimitComeFromThePrintedEffect()
    {
        var abilities = ActivatedAbilityParser.Parse("你可以选择摧毁此装备，以此从任意废牌堆中回收最多三张卡牌。");
        var ability = Assert.Single(abilities);
        Assert.Equal(ActivatedAbilityKinds.DestroySourceRecycleGraveyard, ability.Kind);
        Assert.True(ability.DestroysSourceAsCost); Assert.False(ability.ExhaustsSourceAsCost);
        Assert.Equal(0, ability.RequiredTargetCount); Assert.Equal(3, ability.MaximumTargetCount);
        Assert.Empty(ActivatedAbilityParser.Parse("你可以选择摧毁一名单位，以此从任意废牌堆中回收最多三张卡牌。"));
    }
}
