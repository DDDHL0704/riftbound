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
        var initial = StandbySourceRulesTests.Position("OGN·135/298");
        initial = initial with
        {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                ["LEGEND"] = new("LEGEND", cardNo: card, ownerId: "P1", controllerId: controller)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
            {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
            {
                ["LEGEND"] = new("P1", "LEGEND_ZONE")
            },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(1, 0) }
        };
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

    [Fact]
    public async Task PrintedStandbyCanRecycleRuneAsItsAttachedPowerPayment()
    {
        var initial = StandbySourceRulesTests.Position("OGN·135/298");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["P1-PAY-RUNE"] = new(
                "P1-PAY-RUNE",
                cardNo: "UNL-R03",
                ownerId: "P1",
                controllerId: "P1",
                tags: [CardObjectTags.RuneCard, $"COLOR:{RuneTrait.Red}"])
        };
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
        {
            ["P1"] = initial.PlayerZones["P1"] with { Base = ["P1-PAY-RUNE"] }
        };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
        {
            ["P1-PAY-RUNE"] = new("P1", "BASE")
        };
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = cards,
            PlayerZones = zones,
            ObjectLocations = locations
        };

        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, item => item.Action == CommandTypes.HideCard);
        Assert.True(candidate.Enabled, candidate.Reason);
        Assert.Contains(candidate.OptionalCosts!, choice => choice.Id == "STANDBY_A");
        Assert.Contains(candidate.OptionalCosts!, choice => choice.Id == "RECYCLE_RUNE:P1-PAY-RUNE");
        var requirement = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]);
        var source = Assert.Single(requirement, item => (string)item["sourceObjectId"]! == "SHADOW");
        Assert.Contains(
            Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(source["paymentResourceChoices"]),
            choice => choice.Id == "RECYCLE_RUNE:P1-PAY-RUNE");

        var hidden = await new CoreRuleEngine().ResolveAsync(
            initial,
            new("standby-recycle-payment", "P1", CommandTypes.HideCard),
            new HideCardCommand(
                "SHADOW",
                "OGN·135/298",
                "BATTLEFIELD:F",
                ["STANDBY_A", "RECYCLE_RUNE:P1-PAY-RUNE"]),
            default);
        Assert.True(hidden.Accepted, hidden.ErrorMessage);
        Assert.Equal(RunePool.Empty, hidden.State.RunePools["P1"]);
        Assert.DoesNotContain("P1-PAY-RUNE", hidden.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-PAY-RUNE", hidden.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains(hidden.Events, gameEvent => gameEvent.Kind == "RUNE_RECYCLED");
        var paid = Assert.Single(hidden.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(
            ["RECYCLE_RUNE:P1-PAY-RUNE"],
            Assert.IsType<string[]>(paid.Payload!["paymentResourceActions"]));
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
