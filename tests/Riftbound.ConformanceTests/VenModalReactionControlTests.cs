using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenModalReactionControlTests
{
    private const string Return = "RETURN_FRIENDLY_UNIT";
    private const string Reduce = "ENEMY_UNIT_POWER_MINUS_2";

    [Theory]
    [InlineData(Return, false)] [InlineData(Return, true)]
    [InlineData(Reduce, false)] [InlineData(Reduce, true)]
    public async Task NewTargetsUseTheNewControllerWithoutGrantingANewModeOrReplayingCosts(string mode, bool remake)
    {
        var (initial, driver) = await Open(mode, "OGN·080/298");
        var request = driver.State.PendingRuleChoice!.Request;
        Assert.Equal("SPELL_CHOICE_REMAKE", request.Kind); Assert.Equal("P2", request.PlayerId);
        Assert.DoesNotContain(request.Options, o => o.Label.Contains("模式 ", StringComparison.Ordinal));
        var pool = driver.State.RunePools["P2"];
        var oldTarget = mode == Return ? "ALLY" : "ENEMY";
        var newTarget = mode == Return ? "ENEMY" : "ALLY";
        if (remake) {
            await Choose(driver, RebuttalRulesTests.Option(driver.State, "重选目标 1 为", "（" + newTarget + "）"));
            await Choose(driver, "CONFIRM");
        } else await Choose(driver, "KEEP_ALL");
        Assert.Equal(pool.Mana, driver.State.RunePools["P2"].Mana);
        Assert.Equal(pool.TotalPower, driver.State.RunePools["P2"].TotalPower);
        var spell = Assert.Single(driver.State.StackItems);
        Assert.Equal("P2", spell.ControllerId); Assert.Equal(Effect(mode), spell.EffectKind);
        Assert.Equal([remake ? newTarget : oldTarget], spell.TargetObjectIds);
        await driver.Top();
        if (remake && mode == Return) Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Hand);
        else Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(remake && mode == Reduce ? 2 : 4, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        await driver.Replay(); VenModalReactionTests.Export("control-targets-" + mode + "-" + remake, initial, driver);
    }

    [Theory]
    [InlineData(Return)] [InlineData(Reduce)]
    public async Task AFullChoiceRemakeMayChangeTheModeWhileRetainingItsNowLegalTarget(string originalMode)
    {
        var (initial, driver) = await Open(originalMode, "VEN·152");
        Assert.Equal(20, initial.RunePools["P1"].Mana); Assert.Equal(19, driver.State.RunePools["P1"].Mana);
        Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT", driver.State.PendingRuleChoice!.Request.Kind);
        await Choose(driver, "PAY:ANY");
        Assert.Equal(19, driver.State.RunePools["P1"].Mana);
        Assert.Equal("SPELL_CHOICE_REMAKE", driver.State.PendingRuleChoice!.Request.Kind);
        // The committed checkpoint retains the original action until every
        // synchronous choice is answered; the preview includes the chosen fee.
        Assert.Equal(19, driver.State.RunePools["P2"].TotalPower);
        Assert.Equal(18, driver.State.PendingRuleChoice.Request.Preview!.RunePools["P2"].TotalPower);
        var newMode = originalMode == Return ? Reduce : Return;
        await Choose(driver, RebuttalRulesTests.Option(driver.State, "模式 " + newMode));
        Assert.Equal(19, driver.State.RunePools["P1"].Mana);
        Assert.Equal(18, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P2"].TotalPower);
        await Choose(driver, "CONFIRM");
        Assert.Equal(19, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P2"].Mana);
        Assert.Equal(18, driver.State.RunePools["P2"].TotalPower);
        var spell = Assert.Single(driver.State.StackItems);
        Assert.Equal("P2", spell.ControllerId); Assert.Equal(Effect(newMode), spell.EffectKind);
        Assert.Equal([originalMode == Return ? "ALLY" : "ENEMY"], spell.TargetObjectIds);
        await driver.Top();
        if (newMode == Return) Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Hand);
        else Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")));
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "OPTIONAL_SPELL_CONTROL_PAYMENT")));
        await driver.Replay(); VenModalReactionTests.Export("control-mode-" + originalMode, initial, driver);
    }

    private static async Task<(MatchState Initial, Driver Driver)> Open(string mode, string control)
    {
        var initial = AttackDamageDistributionTests.Spell(VenModalReactionTests.Position(), "CONTROL", control, "P2");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", [mode == Return ? "ALLY" : "ENEMY"], Mode: mode));
        var spellId = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("CONTROL", control, [spellId])); await driver.Top();
        return (initial, driver);
    }

    private static Task Choose(Driver driver, string id)
    {
        var request = driver.State.PendingRuleChoice!.Request;
        return driver.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]));
    }

    private static string Effect(string mode)
        => mode == Return ? "VEN_MODAL_REACTION_RETURN_FRIENDLY_UNIT" : "VEN_MODAL_REACTION_ENEMY_POWER_MINUS_2";
}
