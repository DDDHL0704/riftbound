using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyPaymentPermissionTests
{
    [Theory]
    [InlineData("OGN·263/298", "P1", true)]
    [InlineData("OGN·263a/298", "P1", true)]
    [InlineData("OGN·307/298", "P1", true)]
    [InlineData("OGN·307*/298", "P1", true)]
    [InlineData("OGN·263/298", "P2", false)]
    [InlineData("OGN·263a/298", "P2", false)]
    [InlineData("OGN·307/298", "P2", false)]
    [InlineData("OGN·307*/298", "P2", false)]
    public async Task TeemoReplacementRequiresControlledLegendAndOffersActualManaPayment(string card, string controller, bool allowed)
    {
        var initial = StandbyPaymentState(new RunePool(1, 0), card, controller);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.Equal(allowed, candidate.Enabled);
        Assert.Equal(allowed, candidate.OptionalCosts?.Any(c => c.Id == "STANDBY_TEEMO_MANA") == true);
        var command = new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_TEEMO_MANA"]);
        if (!allowed)
        {
            await Rejected(initial, command);
            return;
        }
        var driver = Driver(initial, "teemo-" + card.Replace('·', '-').Replace('/', '-').Replace('*', 's'));
        var hidden = await driver.Act("P1", command);
        Assert.Equal(new RunePool(0, 0), hidden.State.RunePools["P1"]);
        Assert.Equal("TEEMO", hidden.State.CardObjects["SHADOW"].StandbyPlacement!.Permission);
        var cost = Assert.Single(hidden.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(1, cost.Payload["mana"]);
        Assert.Equal(0, cost.Payload["power"]);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(true, 1, 0, false, false, "STANDBY_TEEMO_MANA")]
    [InlineData(true, 1, 1, false, false, "STANDBY_TEEMO_MANA")]
    [InlineData(true, 1, 1, false, false, "STANDBY_A")]
    [InlineData(false, 1, 1, false, false, "STANDBY_A")]
    [InlineData(true, 0, 0, true, false, "STANDBY_TEEMO_MANA")]
    [InlineData(true, 0, 0, true, true, "STANDBY_TEEMO_MANA")]
    [InlineData(false, 0, 0, true, false, "STANDBY_A")]
    public async Task WebStandbyPromptQuotesAndPaysSelectedFee(
        bool teemo, int mana, int power, bool readyRune, bool afterDuel, string choice)
    {
        const string rune = "READY-RUNE";
        const string tap = "TAP_RUNE:" + rune;
        const string recycle = "RECYCLE_RUNE:" + rune;
        var initial = StandbyPaymentState(new RunePool(mana, power), teemo ? "OGN·263/298" : null);
        if (readyRune)
        {
            initial = WithReadyRune(initial, rune);
        }
        var driver = Driver(initial, "web-standby");
        if (afterDuel)
        {
            await driver.Act("P1", new MoveUnitCommand("SHADOW-OUTSIDE", "BASE", "BATTLEFIELD:OTHER", []));
            Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
            Assert.DoesNotContain(CommandTypes.HideCard, driver.PromptFor("P1").EnabledActions());
            await driver.Act("P1", new PassFocusCommand());
            Assert.DoesNotContain(CommandTypes.HideCard, driver.PromptFor("P2").EnabledActions());
            await driver.Act("P2", new PassFocusCommand());
            Assert.Equal(TimingStates.NeutralOpen, driver.State.TimingState);
        }

        // The web client reads Hide's quote from sourceRequirements and submits directly.
        var prompt = driver.PromptFor("P1");
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.True(candidate.Enabled);
        var requirements = JsonSerializer.SerializeToElement(candidate.Metadata!["sourceRequirements"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var requirement = Assert.Single(requirements.EnumerateArray(),
            r => r.GetProperty("sourceObjectId").GetString() == "SHADOW");
        var destination = Assert.Single(requirement.GetProperty("destinationChoices").EnumerateArray(),
            d => d.GetProperty("id").GetString() == "BATTLEFIELD:F").GetProperty("id").GetString()!;
        var costs = requirement.GetProperty("costsByChoice");
        Assert.Equal(1, requirement.GetProperty("requiredOptionalCostChoiceCount").GetInt32());
        var offered = requirement.GetProperty("optionalCostChoices").EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ToArray();
        Assert.Equal(teemo, offered.Contains("STANDBY_TEEMO_MANA"));
        Assert.Equal(power > 0 || readyRune, offered.Contains("STANDBY_A"));
        if (teemo)
        {
            Assert.Equal(1, costs.GetProperty("STANDBY_TEEMO_MANA").GetProperty("mana").GetInt32());
            Assert.Equal(0, costs.GetProperty("STANDBY_TEEMO_MANA").GetProperty("power").GetInt32());
        }
        if (power > 0 || readyRune)
        {
            Assert.Equal(0, costs.GetProperty("STANDBY_A").GetProperty("mana").GetInt32());
            Assert.Equal(1, costs.GetProperty("STANDBY_A").GetProperty("power").GetInt32());
        }
        var option = costs.GetProperty(choice);
        var manaCost = option.GetProperty("mana").GetInt32();
        var powerCost = option.GetProperty("power").GetInt32();
        var resources = requirement.GetProperty("paymentResourceChoices").EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ToArray();
        Assert.Equal(readyRune && teemo, resources.Contains(tap));
        var payment = new List<string> { choice };
        if (manaCost > mana) { Assert.Contains(tap, resources); payment.Add(tap); }
        if (powerCost > power)
        {
            Assert.Contains(recycle, resources);
            // Table taps a ready rune before recycling it, even for a power-only fee.
            Assert.Contains(CommandTypes.TapRune, prompt.EnabledActions());
            payment.Add(tap);
            payment.Add(recycle);
        }

        var hidden = await driver.Act("P1", new HideCardCommand("SHADOW", "OGN·135/298", destination, payment));
        var pool = hidden.State.RunePools["P1"];
        Assert.Equal(mana + (payment.Contains(tap) ? 1 : 0) - manaCost, pool.Mana);
        Assert.Equal(Math.Max(0, power - powerCost), pool.TotalPower);
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        var cost = Assert.Single(hidden.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(manaCost, cost.Payload["mana"]);
        Assert.Equal(powerCost, cost.Payload["power"]);
        Assert.Equal(manaCost > 0, cost.Payload["teemoStandbyHideReplacement"]);
        Assert.Equal(payment.Skip(1), Assert.IsType<string[]>(cost.Payload["paymentResourceActions"]));
        Assert.Equal(payment.Contains(tap), hidden.Events.Any(e => e.Kind == "RUNE_TAPPED"));
        if (payment.Contains(recycle))
        {
            Assert.Contains(rune, hidden.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal(["RUNE_TAPPED", "RUNE_RECYCLED"],
                hidden.Events.Where(e => e.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED").Select(e => e.Kind));
        }
        else if (payment.Contains(tap))
        {
            Assert.True(hidden.State.CardObjects[rune].IsExhausted);
            Assert.Contains(rune, hidden.State.PlayerZones["P1"].Base);
        }
        await Replay(initial, driver);
    }

    [Fact]
    public async Task StandbyPaymentRejectsRecyclingBeforeTappingTheSameRune()
    {
        const string rune = "READY-RUNE";
        var state = WithReadyRune(StandbyPaymentState(RunePool.Empty, "OGN·263/298"), rune);
        await Rejected(state, new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F",
            ["STANDBY_TEEMO_MANA", "RECYCLE_RUNE:" + rune, "TAP_RUNE:" + rune]));
    }

    private static MatchState WithReadyRune(MatchState state, string rune)
    {
        state = VoluntaryPaymentResourceChoicesTests.WithRune(state, RuneTrait.Purple, rune);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [rune] = state.CardObjects[rune] with { IsExhausted = false } } };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualGuerrillaWarfareResolutionOffersFreeHideAndExpiresAtTurnEnd(bool returnGraveyardCard)
    {
        var initial = StandbySourceRulesTests.Position("OGN·135/298");
        initial = AttackDamageDistributionTests.Spell(initial, "WARFARE", "OGN·264/298", "P1");
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools)
                { ["P1"] = new(2, 0, new Dictionary<string, int> { ["blue"] = 1 }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                { ["SHADOW"] = initial.CardObjects["SHADOW"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] } }
        };
        if (returnGraveyardCard)
        {
            initial = initial with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                {
                    ["P1"] = initial.PlayerZones["P1"] with
                    {
                        Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "SHADOW").ToArray(),
                        Graveyard = initial.PlayerZones["P1"].Graveyard.Append("SHADOW").ToArray()
                    }
                },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
                    { ["SHADOW"] = new("P1", "GRAVEYARD") }
            };
        }
        var driver = Driver(initial, returnGraveyardCard ? "free-return" : "free-zero");
        await driver.Act("P1", new PlayCardCommand("WARFARE", "OGN·264/298", returnGraveyardCard ? ["SHADOW"] : []));
        var resolved = await driver.Top();
        Assert.Contains("FREE_STANDBY_HIDE:P1", resolved.State.UntilEndOfTurnEffects);
        Assert.Contains("SHADOW", resolved.State.PlayerZones["P1"].Hand);
        Assert.Equal(new RunePool(0, 0), resolved.State.RunePools["P1"]);
        var candidate = Assert.Single(resolved.Prompts["P1"].Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.True(candidate.Enabled);
        Assert.Contains(candidate.OptionalCosts!, cost => cost.Id == "STANDBY_FREE");
        var hidden = await driver.Act("P1", new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_FREE"]));
        Assert.Equal(new RunePool(0, 0), hidden.State.RunePools["P1"]);
        Assert.Equal("FREE", hidden.State.CardObjects["SHADOW"].StandbyPlacement!.Permission);
        var cost = Assert.Single(hidden.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(0, cost.Payload["mana"]);
        Assert.Equal(0, cost.Payload["power"]);
        Assert.Equal(true, cost.Payload["standbyHideCostWaived"]);
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain("FREE_STANDBY_HIDE:P1", driver.State.UntilEndOfTurnEffects);
        Assert.True(driver.State.CardObjects["SHADOW"].IsFaceDown);
        await Replay(initial, driver);
    }

    private static MatchState StandbyPaymentState(RunePool pool, string? legend, string controller = "P1")
    {
        var initial = StandbySourceRulesTests.Position("OGN·135/298");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };
        if (legend is null) return initial;
        return initial with
        {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: controller)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
            {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
            {
                ["LEGEND"] = new("P1", "LEGEND_ZONE")
            }
        };
    }

    private static async Task Rejected(MatchState state, GameCommand command)
    {
        var blocked = await new CoreRuleEngine().ResolveAsync(state, new("blocked-payment", "P1", command.CmdType), command, default);
        Assert.False(blocked.Accepted);
        Assert.Empty(blocked.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(blocked.State));
        Assert.Equal(state.RunePools["P1"], blocked.State.RunePools["P1"]);
    }

    private static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length, driver.NativeCommands);
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, accepted = driver.Commands().Length,
            coldRestores = driver.Commands().Length, fullJournalReplay = replay });
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_STANDBY_PERMISSION_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
