using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class InstructionResourceConsumerBoundaryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, false, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, false, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, false, "ZERO")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, false, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, false, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, false, "ZERO")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, true, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, true, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, true, "ZERO")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, false, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, false, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, false, "ZERO")]
    public async Task ConfirmedResourceSkillsCommitTheirOwnCostsAndEffectsWhileBulletTimeRemainsUnpaid(
        string cardNo, bool levelSix, string finish)
    {
        // CN430.2/430.2.a/430.3: the resource skill resolves immediately while
        // the enclosing instruction is suspended. These are scoped midgame
        // fixtures, not ordinary-room birth or SQL/network/restart acceptance.
        var initial = Position(cardNo, levelSix);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
        await driver.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice).Request;
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", request.Kind);
        var option = Assert.Single(request.Options, o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal)
            && o.Id.Contains(P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix, StringComparison.Ordinal) == levelSix);
        var confirmation = await driver.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));

        var mana = cardNo == P4ActivatedAbilityCatalog.DragonSoulSageCardNo || levelSix ? 1 : 0;
        var power = cardNo == P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? 0 : 1;
        AssertResource(driver.State, cardNo, mana, power);
        Assert.Single(driver.State.StackItems);
        Assert.Equal(0, driver.State.CardObjects["TARGET"].Damage);
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", pending.Request.Kind);
        Assert.DoesNotContain(pending.Request.Options, o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal));
        Assert.Single(confirmation.Events, e => e.Kind == "ABILITY_ACTIVATED");
        Assert.Single(confirmation.Events, e => e.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.DoesNotContain(confirmation.Events, e => e.Kind is "COST_PAID" or "DAMAGE_APPLIED");

        // A fresh prompt must not let the exhausted/destroyed source produce
        // resources twice, and rejection cannot alter the committed prefix.
        var before = MatchStateHasher.Hash(driver.State);
        var repeated = await new CoreRuleEngine().ResolveAsync(driver.State,
            new("repeat-confirmed-resource", "P1", CommandTypes.PayCost),
            new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]), default);
        Assert.False(repeated.Accepted);
        Assert.Empty(repeated.Events);
        Assert.Equal(before, MatchStateHasher.Hash(repeated.State));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(driver.State)), MatchStateHasher.HashValue(repeated.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(driver.State)), MatchStateHasher.HashValue(repeated.Prompts));

        var commands = driver.Commands();
        var events = driver.Events();
        var ended = finish == "ZERO"
            ? await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]))
            : await driver.Act(finish, new SurrenderCommand());
        AssertResource(ended.State, cardNo, mana, power);
        Assert.Equal(0, ended.State.CardObjects["TARGET"].Damage);
        Assert.Null(ended.State.PendingRuleChoice);
        Assert.DoesNotContain(ended.Events, e => e.Kind is "ABILITY_ACTIVATED" or "RESOURCE_SKILL_RESOLVED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.Equal(JsonSerializer.Serialize(commands, Json), JsonSerializer.Serialize(driver.Commands().Take(commands.Length), Json));
        Assert.Equal(JsonSerializer.Serialize(events, Json), JsonSerializer.Serialize(driver.Events().Take(events.Length), Json));
        var instructionPayments = ended.Events.Where(e => e.Kind == "COST_PAID"
            && Equals(e.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT")).ToArray();
        if (finish == "ZERO")
        {
            Assert.Equal(MatchStatuses.InProgress, ended.State.Status);
            Assert.Equal(0, Assert.Single(instructionPayments).Payload["totalPowerCost"]);
            Assert.Contains("CARD", ended.State.PlayerZones["P1"].Graveyard);
        }
        else
        {
            Assert.Equal(MatchStatuses.Finished, ended.State.Status);
            Assert.Equal(finish == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
            Assert.Empty(instructionPayments);
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private static void AssertResource(MatchState state, string cardNo, int mana, int power)
    {
        Assert.Equal(mana, state.RunePools["P1"].Mana);
        Assert.Equal(power, state.RunePools["P1"].TotalPower);
        if (cardNo == P4ActivatedAbilityCatalog.GoldTokenUnlCardNo)
        {
            Assert.False(state.CardObjects.ContainsKey("RESOURCE"));
            Assert.False(state.ObjectLocations.ContainsKey("RESOURCE"));
            Assert.DoesNotContain("RESOURCE", state.PlayerZones["P1"].Base);
            Assert.DoesNotContain("RESOURCE", state.PlayerZones["P1"].Graveyard);
        }
        else
        {
            Assert.True(state.CardObjects["RESOURCE"].IsExhausted);
            Assert.Contains("RESOURCE", state.PlayerZones["P1"].Base);
            Assert.Equal("BASE", state.ObjectLocations["RESOURCE"].Zone);
        }
    }

    private static MatchState Position(string cardNo, bool levelSix)
    {
        var card = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", "OGN·268/298", "P1");
        var field = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·280/298", "P2") with { ControllerId = "P2" };
        var target = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·142/298", "P2");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target.CardNo!, out var behavior));
        target = target with { Tags = target.Tags.Concat(behavior.SourceUnitTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray() };
        var resource = P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var token)
            ? token.CreateObject("RESOURCE", "P1", "P1", false)
            : OfficialGarenConquestBattlefieldCountTests.Printed("RESOURCE", cardNo, "P1");
        return new("INSTRUCTION-RESOURCE-CONSUMER", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(1, 0), ["P2"] = RunePool.Empty },
            playerExperience: new Dictionary<string, int> { ["P1"] = levelSix ? 6 : 0, ["P2"] = 0 },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Base = ["RESOURCE"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = card, ["FIELD"] = field, ["TARGET"] = target, ["RESOURCE"] = resource },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["RESOURCE"] = new("P1", "BASE"),
                ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), ["TARGET"] = new("P2", "BATTLEFIELD", "FIELD") });
    }
}
