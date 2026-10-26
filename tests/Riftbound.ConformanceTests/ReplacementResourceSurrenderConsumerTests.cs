using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReplacementResourceSurrenderConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, "P2")]
    [InlineData(P4ActivatedAbilityCatalog.RageSigilCardNo, "P1")]
    [InlineData(P4ActivatedAbilityCatalog.RageSigilCardNo, "P2")]
    public async Task SurrenderKeepsTheConfirmedResourceButDoesNotCommitTheSuspendedDestruction(
        string cardNo, string surrenderingPlayer)
    {
        // Explicit midgame fixtures: CN430 resource completion and CN650/651
        // concession boundaries, not ordinary-room birth or L3 acceptance.
        var initial = ReplacementResourceTests.AddResource(OfficialSettReplacementTests.Position(power: 0), cardNo);
        initial = initial with { PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P2"] = 0 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await driver.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("P2", pending.Request.PlayerId);
        Assert.Equal("DESTRUCTION_REPLACEMENT", pending.Request.Kind);
        var origin = MatchStateHasher.Hash(pending.Origin);
        var target = MatchStateHasher.HashValue(driver.State.CardObjects["D"]);
        var legend = MatchStateHasher.HashValue(driver.State.CardObjects["LEGEND"]);
        Assert.Equal(1, driver.State.CardObjects["D"].Damage);
        var option = Assert.Single(pending.Request.Options,
            o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal) && o.Id.EndsWith(':'));
        var confirmed = await driver.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]));
        AssertResource(confirmed.State, cardNo);
        var next = Assert.IsType<RuleChoiceContinuation>(confirmed.State.PendingRuleChoice);
        Assert.Equal(origin, MatchStateHasher.Hash(next.Origin));
        Assert.Equal(target, MatchStateHasher.HashValue(confirmed.State.CardObjects["D"]));
        Assert.Equal(legend, MatchStateHasher.HashValue(confirmed.State.CardObjects["LEGEND"]));
        Assert.Contains("D", confirmed.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("D", confirmed.State.PlayerZones["P2"].Base);
        Assert.Single(confirmed.Events, e => e.Kind == "ABILITY_ACTIVATED");
        Assert.Single(confirmed.Events, e => e.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.DoesNotContain(confirmed.Events, e => e.Kind is "COST_PAID" or "DAMAGE_APPLIED" or "UNIT_RECALLED_TO_BASE");
        Assert.DoesNotContain(next.Request.Options, o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal));
        var isGold = cardNo is P4ActivatedAbilityCatalog.GoldTokenUnlCardNo or P4ActivatedAbilityCatalog.GoldTokenSfdCardNo;
        if (isGold) Assert.Single(confirmed.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");

        var commands = driver.Commands();
        var events = driver.Events();
        var ended = await driver.Act(surrenderingPlayer, new SurrenderCommand());
        Assert.Equal(MatchStatuses.Finished, ended.State.Status);
        Assert.Equal(surrenderingPlayer == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        AssertResource(ended.State, cardNo);
        Assert.Equal(target, MatchStateHasher.HashValue(ended.State.CardObjects["D"]));
        Assert.Equal(legend, MatchStateHasher.HashValue(ended.State.CardObjects["LEGEND"]));
        Assert.Contains("D", ended.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("D", ended.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("D", ended.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("MATCH_WON", Assert.Single(ended.Events).Kind);
        Assert.Equal(JsonSerializer.Serialize(commands, Json), JsonSerializer.Serialize(driver.Commands().Take(commands.Length), Json));
        Assert.Equal(JsonSerializer.Serialize(events, Json), JsonSerializer.Serialize(driver.Events().Take(events.Length), Json));
        Assert.Single(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        if (isGold) Assert.Single(driver.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private static void AssertResource(MatchState state, string cardNo)
    {
        Assert.Equal(7, state.RunePools["P2"].Mana);
        Assert.Equal(1, state.RunePools["P2"].TotalPower);
        Assert.Equal(cardNo == P4ActivatedAbilityCatalog.RageSigilCardNo ? 1 : 0,
            state.RunePools["P2"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        if (cardNo is P4ActivatedAbilityCatalog.GoldTokenUnlCardNo or P4ActivatedAbilityCatalog.GoldTokenSfdCardNo)
        {
            Assert.False(state.CardObjects.ContainsKey("RESOURCE"));
            Assert.False(state.ObjectLocations.ContainsKey("RESOURCE"));
            Assert.DoesNotContain("RESOURCE", state.PlayerZones["P2"].Base);
            Assert.DoesNotContain("RESOURCE", state.PlayerZones["P2"].Graveyard);
        }
        else
        {
            Assert.True(state.CardObjects["RESOURCE"].IsExhausted);
            Assert.Contains("RESOURCE", state.PlayerZones["P2"].Base);
            Assert.Equal("BASE", state.ObjectLocations["RESOURCE"].Zone);
        }
    }
}
