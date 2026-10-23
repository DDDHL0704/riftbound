using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenModalReactionBoundaryTests
{
    [Fact]
    public void TheNativePromptKeepsTwoHumanReadableModesAndSeparateTargetDomains()
    {
        var initial = VenModalReactionTests.Position(); var prompts = ResolutionResult.BuildPrompts(initial);
        var candidate = Assert.Single(prompts["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var modes = candidate.Modes!.ToDictionary(c => c.Id);
        Assert.Equal("让一名友方单位返回其拥有者的手牌", modes["RETURN_FRIENDLY_UNIT"].Label);
        Assert.Equal("让一名敌方单位本回合战力 -2", modes["ENEMY_UNIT_POWER_MINUS_2"].Label);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"])
            .Where(r => Equals(r["sourceObjectId"], "SPELL")).ToArray();
        Assert.Equal(2, requirements.Length);
        foreach (var row in requirements) {
            var mode = Assert.IsType<string>(row["mode"]);
            Assert.Equal(1, row["minTargetCount"]); Assert.Equal(1, row["maxTargetCount"]);
            var targets = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(row["targetChoicesByIndex"]);
            var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targets["0"]).Select(c => c.Id).ToArray();
            Assert.Contains(mode == "RETURN_FRIENDLY_UNIT" ? "ALLY" : "ENEMY", choices);
            Assert.DoesNotContain(mode == "RETURN_FRIENDLY_UNIT" ? "ENEMY" : "ALLY", choices);
        }
        var opponent = JsonSerializer.Serialize(prompts["P2"]);
        Assert.DoesNotContain("SPELL", opponent); Assert.DoesNotContain("RETURN_FRIENDLY_UNIT", opponent);
    }

    [Theory]
    [InlineData("RETURN_FRIENDLY_UNIT", false)] [InlineData("ENEMY_UNIT_POWER_MINUS_2", true)]
    public async Task WardDependsOnTheChosenModesEnemyTargetRatherThanThePhysicalCardsOwnership(string mode, bool tax)
    {
        var initial = VenModalReactionTests.Position(); var target = tax ? "ENEMY" : "ALLY";
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { OwnerId = tax ? "P1" : "P2",
                Tags = [..initial.CardObjects[target].Tags, CardObjectTags.Spellshield] } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", [target], Mode: mode));
        Assert.Equal(tax ? 8 : 9, driver.State.RunePools["P1"].TotalPower);
        await driver.Top();
        if (!tax) Assert.Contains(target, driver.State.PlayerZones["P2"].Hand);
        else Assert.Equal(2, driver.State.CardObjects[target].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RETURN_FRIENDLY_UNIT")] [InlineData("ENEMY_UNIT_POWER_MINUS_2")]
    public async Task ReactionPermissionDoesNotLetEitherModeActWhileTheOpponentHoldsPriority(string mode)
    {
        var initial = AttackDamageDistributionTests.Spell(VenModalReactionTests.Position() with {
            TurnPlayerId = "P2", ActivePlayerId = "P2" }, "PENDING", "OGN·048/298", "P2");
        var driver = new Driver(initial);
        await driver.Act("P2", new PlayCardCommand("PENDING", "OGN·048/298", []));
        Assert.Equal("P2", driver.State.PriorityPlayerId);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new PlayCardCommand("SPELL", "VEN·052",
            [mode == "RETURN_FRIENDLY_UNIT" ? "ALLY" : "ENEMY"], Mode: mode));
        await driver.Top(); await driver.Replay();
    }

    [Theory]
    [InlineData("RETURN_FRIENDLY_UNIT", true)] [InlineData("ENEMY_UNIT_POWER_MINUS_2", false)]
    public async Task OnePrintedPowerPaysForTheFriendlyModeButCannotAlsoPayAnEnemyWardTax(string mode, bool allowed)
    {
        var target = allowed ? "ALLY" : "ENEMY"; var initial = VenModalReactionTests.Position();
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [target] = initial.CardObjects[target] with { Tags = [..initial.CardObjects[target].Tags, CardObjectTags.Spellshield] } } };
        var command = new PlayCardCommand("SPELL", "VEN·052", [target], Mode: mode);
        if (!allowed) { await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", command); return; }
        var driver = new Driver(initial); await driver.Act("P1", command); await driver.Top();
        Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); await driver.Replay();
    }

    [Fact]
    public async Task ASelectedModeCannotBeReplacedByTheOtherCoherentEffectWithoutChangingTheOriginalHistory()
    {
        var initial = VenModalReactionTests.Position(); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", ["ALLY"], Mode: "RETURN_FRIENDLY_UNIT"));
        var item = Assert.Single(driver.State.StackItems); var hash = MatchStateHasher.Hash(driver.State);
        var forged = driver.State with { StackItems = [item with { EffectKind = "VEN_MODAL_REACTION_ENEMY_POWER_MINUS_2",
            TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = driver.State.CardObjects["ENEMY"].ObjectGeneration } }] };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.Equal(hash, MatchStateHasher.Hash(driver.State));
        await driver.Top(); Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RETURN_FRIENDLY_UNIT")] [InlineData("ENEMY_UNIT_POWER_MINUS_2")]
    public async Task ReturningTheOldTargetBeforeResolutionDoesNotApplyEitherModeToItsNewPrivateIncarnation(string mode)
    {
        var player = mode == "RETURN_FRIENDLY_UNIT" ? "P1" : "P2";
        var target = player == "P1" ? "ALLY" : "ENEMY";
        var initial = AttackDamageDistributionTests.Spell(VenModalReactionTests.Position(), "RETURN", "OGN·104/298", player);
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", [target], Mode: mode));
        if (player == "P2") await driver.Act("P1", new PassPriorityCommand());
        var generation = driver.State.CardObjects[target].ObjectGeneration;
        await driver.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", [target])); await driver.Top();
        Assert.Contains(target, driver.State.PlayerZones[player].Hand); Assert.True(driver.State.CardObjects[target].ObjectGeneration > generation);
        await driver.Top(); Assert.Equal(4, driver.State.CardObjects[target].Power);
        Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnPowerModifiers);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); await driver.Replay();
    }
}
