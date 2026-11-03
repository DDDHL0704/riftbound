using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed partial class RadBlastAmplifierTests
{
    private const string Amplifier = "RAD·017/167";
    private const string Kill = "OGN·229/298";

    [Fact]
    public async Task RealDeploymentPaysFourManaAndOneRedBeforeItsOptionalTrigger()
    {
        var card = Assert.Single(RadiantCardSources.GetReviewedPhysicalPrints(), c => c.CardNo == Amplifier);
        Assert.Equal(39507, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = RunePool.Empty } };
        var game = new Driver(initial); await game.Act("P1", Play());
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("BF", game.State.ObjectLocations["AMP"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["AMP"].Tags);
        Assert.NotNull(game.State.PendingCardChoice); Assert.Null(game.State.PriorityPlayerId);
        await Pay(game, []); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(3, RuneTrait.Red)] [InlineData(4, RuneTrait.Blue)] [InlineData(4, "")]
    public async Task MissingPrintedResourcesRejectAtomically(int mana, string trait)
        => await Reject(Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(mana, 0, trait.Length == 0 ? null : new Dictionary<string, int> { [trait] = 1 }),
            ["P2"] = RunePool.Empty } }, "P1", Play());

    [Theory]
    [InlineData("BASE")] [InlineData("")] [InlineData("BATTLEFIELD:ABSENT")]
    public async Task DeploymentRequiresAnExistingBattlefield(string destination)
        => await Reject(Position(), "P1", new PlayCardCommand("AMP", Amplifier, [], Destination: destination));

    [Theory]
    [InlineData(false, "P1")] [InlineData(true, "P1")] [InlineData(false, "P2")]
    public async Task OtherFriendlyPermanentsPayBeforeResponsesWithoutBecomingTargets(bool equipment, string owner)
    {
        var initial = Put(Position(), "COST", equipment ? "OGN·040/298" : "OGN·219/298", owner, "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "COST", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["COST"] = initial.CardObjects["COST"] with { Tags = initial.CardObjects["COST"].Tags
                .Append("法盾2").Distinct().Order(StringComparer.Ordinal).ToArray() } } };
        var game = new Driver(initial); await game.Act("P1", Play());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow); Assert.Contains("COST", choice.LegalObjectIds);
        Assert.DoesNotContain("AMP", choice.LegalObjectIds); Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId); var hand = game.State.PlayerZones["P1"].Hand.ToArray();
        var pool = game.State.RunePools["P1"];
        await Pay(game, ["COST"]);
        Assert.Contains("COST", game.State.PlayerZones[owner].Graveyard);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Equal("COST", Assert.Single(skill.TriggerCost!.DestroyedPermanents!).ObjectId);
        Assert.Equal(pool, game.State.RunePools["P1"]); Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
        Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top(); Assert.Equal(hand.Length + 1, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData("AMP")] [InlineData("ENEMY")] [InlineData("P1-D0")] [InlineData("UNKNOWN")]
    public async Task InvalidCostSelectionsNeverChangeTheUnconfirmedSkill(string target)
    {
        var game = new Driver(Position()); await game.Act("P1", Play());
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "ALLY"]));
        await Pay(game, []); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task AnUnpayableOptionalPlayTriggerDisappearsWithoutUndoingDeployment()
    {
        var initial = Position(); initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = [] } },
            CardObjects = initial.CardObjects.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value) };
        var game = new Driver(initial); await game.Act("P1", Play());
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("AMP", game.State.PlayerZones["P1"].Battlefields); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualDestructionReplacementPaysTheLeadingCostWithoutLastBreath(bool replace)
    {
        var initial = Put(Position(), "COST", "OGN·096/298", "P1", "BASE");
        if (replace) initial = Put(initial, "GLASS", "OGN·077/298", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", Play()); var hand = game.State.PlayerZones["P1"].Hand.Count;
        await Pay(game, ["COST"]);
        if (game.State.PendingRuleChoice is { } pending) {
            Assert.True(replace);
            var replacement = Assert.Single(pending.Request.Options, o => o.Id.StartsWith("GEAR:", StringComparison.Ordinal)
                && o.ObjectIds?.Contains("GLASS") == true);
            await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [replacement.Id]));
        }
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(replace ? 0 : 1, Draws(game.State).Length);
        Assert.Contains("COST", replace ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Graveyard);
        var paid = Assert.Single(game.State.StackItems, i => i.UnitEvent?.CardNo == Amplifier);
        Assert.Equal("COST", Assert.Single(paid.TriggerCost!.DestroyedPermanents!).ObjectId);
        await ResolveAllDraws(game); Assert.Equal(hand + (replace ? 1 : 2), game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task PaidReceiptRejectsSourcePaymentMissingBindingAndFutureGeneration()
    {
        var game = new Driver(Position()); await game.Act("P1", Play()); await Pay(game, ["ALLY"]);
        var item = Assert.Single(game.State.StackItems); var receipt = item.TriggerCost!;
        foreach (var invalid in new[] { receipt with { DestroyedPermanents = null },
            receipt with { DestroyedPermanents = [receipt.Source] },
            receipt with { DestroyedPermanents = [new("ALLY", game.State.CardObjects["ALLY"].ObjectGeneration + 1)] } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { TriggerCost = invalid }] }));
        await game.Top(); await game.Replay();
    }

    private static MatchState Position() => Put(Create("OGN·278/298"), "AMP", Amplifier, "P1", "HAND");
    private static PlayCardCommand Play() => new("AMP", Amplifier, [], Destination: "BATTLEFIELD:BF");
    private static async Task Deploy(Driver game)
    { await game.Act("P1", Play()); if (game.State.PendingCardChoice is not null) await Pay(game, []); }
    private static Task Pay(Driver game, IReadOnlyList<string> ids)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }
    private static LastBreathDrawContext[] Draws(MatchState state) => state.TriggerQueue.Select(i => i.LastBreathDraw)
        .Concat(state.StackItems.Select(i => i.LastBreathDraw)).OfType<LastBreathDrawContext>().ToArray();
    private static async Task ResolveAllDraws(Driver game)
    {
        for (var step = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; step++) {
            Assert.InRange(step, 0, 12);
            if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
                OrderedTriggerIds: game.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
            else await game.Top();
        }
    }
}
