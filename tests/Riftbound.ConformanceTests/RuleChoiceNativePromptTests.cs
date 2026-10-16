using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuleChoiceNativePromptTests
{
    [Theory]
    [InlineData("SFD·029/221", false)]
    [InlineData("SFD·029/221", true)]
    [InlineData("SFD·029a/221", false)]
    [InlineData("SFD·029a/221", true)]
    public async Task ActualTokenResourceDecisionsRemainReachableAndAtomicThroughThePrompt(string grant, bool exhausted)
    {
        var initial = GrantedTokenHasteTests.Position(grant);
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(exhausted ? 3 : 2, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["CHOICE-RUNE"] = new("CHOICE-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                    isExhausted: exhausted, tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "CHOICE-RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["CHOICE-RUNE"] = new("P1", "BASE") }
        };
        var driver = Driver(initial, "token-" + (grant.Contains('a') ? "alternate" : "normal") + "-" + exhausted);
        await driver.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", []));
        await driver.Top();
        Assert.Equal("P1", driver.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Equal("P2", driver.State.PendingRuleChoice.Intent.PlayerId);
        if (!exhausted) await Choose("TAP_RUNE:CHOICE-RUNE");
        await Choose("RECYCLE_RUNE:CHOICE-RUNE");
        Assert.Contains("CHOICE-RUNE", driver.State.PendingRuleChoice!.Request.Preview!.PlayerZones["P1"].RuneDeck);
        await Choose("PAY:TRAIT:red");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("CHOICE-RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("CHOICE-RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.False(Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), "TOKEN_HASTE_PAYMENT"));
        await Finish(initial, driver, exhausted ? 2 : 3);

        async Task Choose(string id)
        {
            var pending = driver.State.PendingRuleChoice!.Request;
            var prompts = ResolutionResult.BuildPrompts(driver.State);
            var candidate = Assert.Single(prompts["P1"].Candidates!, c => c.Action == CommandTypes.PayCost);
            driver.Export("pending-prompt.json", prompts["P1"]);
            var step = "defend-step-" + driver.Commands().Length;
            driver.Export(step + "-opponent-snapshot.json", ResolutionResult.BuildSnapshots(driver.State)["P2"]);
            AssertAllAtomicOptions(pending, candidate);
            Assert.Contains(candidate.SelectionSteps!.Single().Choices, c => c.Id == id);
            Assert.DoesNotContain(prompts["P2"].Candidates!, c => c.Action == CommandTypes.PayCost && c.Enabled);
            // A resource action is one synchronous decision, never combined with PAY or DECLINE.
            var hash = MatchStateHasher.Hash(driver.State);
            foreach (var (actor, options) in new[] { ("P2", new[] { id }), ("P1", new[] { id, "DECLINE" }), ("P1", new[] { "RECYCLE_RUNE:FORGED" }) })
            {
                var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("forged-resource", actor, CommandTypes.PayCost),
                    new PayCostCommand(pending.Id, "RULE_REPLACEMENT", options), default);
                Assert.False(rejected.Accepted);
                Assert.Empty(rejected.Events);
                Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
            }
            await driver.Act("P1", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", [id]));
        }
    }

    [Theory]
    [InlineData("ordinary", "STANDBY_A")]
    [InlineData("teemo", "STANDBY_TEEMO_MANA")]
    [InlineData("free", "STANDBY_FREE")]
    public async Task EveryActualHidePermissionRequiresLocationAndCostBeforeSubmission(string branch, string fee)
    {
        var initial = StandbyTestDriver.WithControlledBattlefield(StandbySourceRulesTests.Position("OGN·135/298"));
        initial = OfficialHoldSequenceTests.AddUnit(initial, "SECOND-FIELD-ANCHOR", "OGN·001/298", StandbyTestDriver.BattlefieldId, "P1");
        if (branch == "teemo") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(1, 1) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["CHOICE-LEGEND"] = new("CHOICE-LEGEND", cardNo: "OGN·263/298", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["CHOICE-LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["CHOICE-LEGEND"] = new("P1", "LEGEND_ZONE") }
        };
        if (branch == "free") {
            initial = AttackDamageDistributionTests.Spell(initial, "CHOICE-WARFARE", "OGN·264/298", "P1");
            initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        }
        var driver = Driver(initial, "hide-" + branch);
        if (branch == "free") {
            await driver.Act("P1", new PlayCardCommand("CHOICE-WARFARE", "OGN·264/298", []));
            await driver.Top();
            Assert.Contains("FREE_STANDBY_HIDE:P1", driver.State.UntilEndOfTurnEffects);
        }
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.HideCard);
        driver.Export("pending-prompt.json", prompt);
        Assert.True(candidate.Enabled);
        Assert.True(candidate.CommandTemplate!.Bindings.Single(b => b.Field == "destination").Required);
        Assert.True(candidate.CommandTemplate.Bindings.Single(b => b.Field == "optionalCosts").Required);
        Assert.True(candidate.SelectionSteps!.Single(s => s.Role == "destination").Required);
        Assert.True(candidate.SelectionSteps!.Single(s => s.Role == "optionalCost").Required);
        Assert.True(candidate.SelectionSteps!.Single(s => s.Role == "destination").Choices.Count >= 2);
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == fee);
        if (branch == "teemo") Assert.Equal(2, candidate.OptionalCosts!.Count);
        var before = driver.State;
        var hash = MatchStateHasher.Hash(before);
        foreach (var command in new[] {
            new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F", []),
            new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:FORGED", [fee]),
            new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_FORGED_FREE"]) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(before, new("forged-hide", "P1", CommandTypes.HideCard), command, default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        }
        var hidden = await driver.Act("P1", new HideCardCommand("SHADOW", "OGN·135/298",
            "BATTLEFIELD:" + StandbyTestDriver.BattlefieldId, [fee]));
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal(StandbyTestDriver.BattlefieldId, hidden.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        var cost = Assert.Single(hidden.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(branch == "teemo" ? 1 : 0, cost.Payload["mana"]);
        Assert.Equal(branch == "ordinary" ? 1 : 0, cost.Payload["power"]);
        StandbyTestDriver.AssertHiddenBattlefieldPrivacy(hidden, "SHADOW");
        // Ordinary play's extra costs remain optional; HIDE alone requires its placement fee choice.
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.False(play.CommandTemplate!.Bindings.Single(b => b.Field == "optionalCosts").Required);
        await Finish(initial, driver, 1);
    }

    private static void AssertAllAtomicOptions(RuleChoiceRequest request, ActionPromptCandidateDto candidate)
    {
        var step = Assert.Single(candidate.SelectionSteps!);
        Assert.True(step.Required);
        Assert.Equal("target", step.Role);
        Assert.Equal(request.Options.Select(o => o.Id).Order(), step.Choices.Select(o => o.Id).Order());
        var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(request.Options.Select(o => o.Id).Order(), metadata.GetProperty("paymentChoices").EnumerateArray().Select(o => o.GetProperty("id").GetString()).Order());
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_NATIVE_PROMPT_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }

    private static async Task Finish(MatchState initial, StandbyDefendObserverTests.Driver driver, int expectedNative)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_NATIVE_PROMPT_READBACK") == "1")
            Assert.Equal(expectedNative, driver.NativeCommands);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, commands = driver.Commands().Length,
            coldRestores = driver.Commands().Length, fullActionEventStateReplayMatched = replay.IsMatch,
            finalHash = MatchStateHasher.Hash(driver.State), fixturePositionOnlyNotAnOriginalEmptyOpening = true });
    }
}
