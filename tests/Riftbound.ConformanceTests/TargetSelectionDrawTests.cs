using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TargetSelectionDrawTests
{
    private const string Jar = "SFD·142/221";
    private const string Wheel = "SFD·144/221";
    private const string Boost = "SFD·034/221";

    private static MatchState Position(string card)
        => AttackDamageDistributionTests.Spell(
            RadGearTestPosition.Put(RadGearTestPosition.Create(), "SOURCE", card, "P1", "BASE"),
            "SPELL", Boost, "P1");

    private static Task Pay(Driver game, bool accept = true) => IreliaTargetSelectionTriggerTests.Pay(game, accept);

    [Fact]
    public async Task JarDrawsWhenItsControllerConfirmsItAsASpellTargetBeforeThatSpellResolves()
    {
        var game = new Driver(Position(Jar)); var power = game.State.CardObjects["SOURCE"].Power;
        await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["SOURCE"]));
        Assert.Equal(2, game.State.StackItems.Count); Assert.Null(game.State.PendingPayment);
        var trigger = game.State.StackItems[^1]; Assert.Equal("CAPTURED_UNIT_EVENT_ABILITY", trigger.EffectKind);
        Assert.True(trigger.UnitEvent!.TargetSelection!.Spell);
        Assert.Equal("SOURCE", trigger.UnitEvent.Other!.Binding.ObjectId); Assert.Empty(trigger.TargetObjectIds);
        await game.Top(); Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(power, game.State.CardObjects["SOURCE"].Power);
        await game.Top(); Assert.Equal(power + 2, game.State.CardObjects["SOURCE"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("other-unit")] [InlineData("opponent")] [InlineData("ability")]
    public async Task JarIgnoresOtherUnitsOpponentsAndNonSpellAbilities(string branch)
    {
        var initial = Position(Jar);
        if (branch == "opponent") {
            initial = AttackDamageDistributionTests.Spell(initial, "THEIR", Boost, "P2");
            initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        }
        if (branch == "ability") initial = RadGearTestPosition.Put(initial, "FLOCK", "RAD·027/167", "P1", "BASE");
        var game = new Driver(initial);
        if (branch == "ability") {
            var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "RAD·027/167"
                && a.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower);
            await game.Act("P1", new ActivateAbilityCommand("FLOCK", ability.AbilityId, ["SOURCE"]));
        } else await game.Act(branch == "opponent" ? "P2" : "P1",
            new PlayCardCommand(branch == "opponent" ? "THEIR" : "SPELL", Boost,
                [branch == "other-unit" ? "ALLY" : "SOURCE"]));
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingPayment); await game.Top();
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningJarInResponseKeepsBothEarnedDrawsAndMasksTheDepartedSource()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(Jar), "RETURN", "OGN·104/298", "P1"));
        await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["SOURCE"]));
        var original = game.State.StackItems[^1].StackItemId;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
        await game.Top(); await game.Top(); Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand);
        var pending = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var captured = Assert.Single(pending.EnumerateArray(), item => item.GetProperty("stackItemId").GetString() == original);
        Assert.DoesNotContain("SOURCE", JsonSerializer.Serialize(captured));
        await game.Top(); await game.Top();
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARD_DRAWN")); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WheelPaysOneManaAndExhaustsForSpellOrAbilityTargetsBeforeDrawing(bool ability)
    {
        var initial = Position(Wheel);
        if (ability) initial = RadGearTestPosition.Put(initial, "FLOCK", "RAD·027/167", "P1", "BASE");
        var game = new Driver(initial);
        if (ability) {
            var action = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "RAD·027/167"
                && a.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower);
            await game.Act("P1", new ActivateAbilityCommand("FLOCK", action.AbilityId, ["ALLY"]));
        } else await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["ALLY"]));
        Assert.Equal(1, game.State.PendingPayment!.ManaCost); Assert.Equal(0, game.State.PendingPayment.PowerCost);
        var pool = game.State.RunePools["P1"]; Assert.False(game.State.CardObjects["SOURCE"].IsExhausted);
        await Pay(game); Assert.Equal(pool.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["SOURCE"].IsExhausted);
        Assert.DoesNotContain("P1-D0", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("decline")] [InlineData("no-mana")] [InlineData("exhausted")]
    [InlineData("enemy-target")] [InlineData("opponent")]
    public async Task WheelRequiresTheFriendlyTargetAndAnAcceptedPayableCost(string branch)
    {
        var initial = Position(Wheel);
        if (branch == "no-mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(2, 30) } };
        if (branch == "exhausted") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SOURCE"] = initial.CardObjects["SOURCE"] with { IsExhausted = true } } };
        if (branch == "opponent") {
            initial = AttackDamageDistributionTests.Spell(initial, "THEIR", Boost, "P2");
            initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        }
        var game = new Driver(initial); await game.Act(branch == "opponent" ? "P2" : "P1",
            new PlayCardCommand(branch == "opponent" ? "THEIR" : "SPELL", Boost, [branch == "enemy-target" ? "ENEMY" : "ALLY"]));
        if (branch is "decline" or "no-mana") await Pay(game, false);
        Assert.Null(game.State.PendingPayment); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(branch == "exhausted", game.State.CardObjects["SOURCE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task RepeatedTargetSelectionsEarnSeparateWheelTriggersButCanOnlyExhaustOnce()
    {
        var game = new Driver(Position(Wheel));
        await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["ALLY"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["ALLY"])]));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Pay(game); await game.Top(); Assert.Null(game.State.PendingPayment); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("not-spell")]
    public async Task RecoveryRejectsErasedDuplicatedOrForgedTargetSelectionCaptures(string branch)
    {
        var game = new Driver(Position(Jar)); await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["SOURCE"]));
        var state = game.State; var spell = state.StackItems[0]; var skill = state.StackItems[1];
        var forged = state with { StackItems = branch switch {
            "erase" => [spell], "duplicate" => [spell, skill, skill],
            _ => [spell, skill with { UnitEvent = skill.UnitEvent! with {
                TargetSelection = skill.UnitEvent.TargetSelection! with { Spell = false } } }] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
