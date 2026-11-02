using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNazumahHonorLegendTests
{
    private const string Ability = LegendActionAbilityCatalog.KsanteLegendAbilityId;
    private const string Grant = "COMBAT_KEYWORD:强攻等同于坚守";

    private static MatchState WithLegend(MatchState state, string cardNo = "RAD·147/167")
        => state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cardNo, "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND")
            }
        };

    private static MatchState Position(string legend = "RAD·147/167", string unit = "RAD·031/167")
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("OGN·219/298"), legend);
        var ally = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", unit, "P1");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(unit, out var behavior));
        ally = ally with { Tags = ally.Tags.Concat(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray() };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ALLY"] = ally } };
    }

    private static LegendActCommand Activate(string target = "ALLY")
        => new("LEGEND", Ability, [target], ["SPEND_MANA:1"]);
    private static int Keyword(MatchState state, string target, string keyword)
        => RadCombatKeywordDoubleTests.Keyword(state, target, keyword);

    [Theory]
    [InlineData("RAD·147/167", 39655L, false)]
    [InlineData("RAD·147/167", 39655L, true)]
    [InlineData("RAD·172*/167", 39684L, false)]
    [InlineData("RAD·172*/167", 39684L, true)]
    [InlineData("RAD·172/167", 39685L, false)]
    [InlineData("RAD·172/167", 39685L, true)]
    public async Task EveryPrintingPaysBeforeResponsesAndGrantsToAFriendlyUnitInBaseOrBattlefield(string no, long id, bool battlefield)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal("纳祖芒荣耀", card.CardName);
        Assert.Contains("等同于自身{{坚守}}的{{强攻}}", card.CardEffect);
        Assert.Contains(no, LegendActionAbilityCatalog.SourceCardNosForAbility(Ability));
        var initial = Position(no);
        if (battlefield) initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, Keyword(game.State, "ALLY", "强攻"));
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendUnitKeyword);
        await game.Top();
        Assert.Equal(3, Keyword(game.State, "ALLY", "坚守"));
        Assert.Equal(3, Keyword(game.State, "ALLY", "强攻"));
        Assert.Contains(Grant + ":1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", 0)]
    [InlineData("RAD·031/167", 3)]
    public async Task LaterSteadfastGrantsChangeAssaultAndTurnCleanupRemovesTheGrantedText(string unit, int original)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(unit: unit), "AXE", "RAD·148/167");
        var game = new Driver(initial);
        await game.Act("P1", Activate());
        await game.Top();
        Assert.Contains(Grant + ":1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(original, Keyword(game.State, "ALLY", "强攻"));
        await game.Act("P1", new PlayCardCommand("AXE", "RAD·148/167", ["ALLY", "ENEMY"]));
        await game.Top();
        Assert.Equal(original + 2, Keyword(game.State, "ALLY", "坚守"));
        Assert.Equal(original + 2, Keyword(game.State, "ALLY", "强攻"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(original, Keyword(game.State, "ALLY", "坚守"));
        Assert.Equal(0, Keyword(game.State, "ALLY", "强攻"));
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(Grant, StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", 0, 1)]
    [InlineData("RAD·031/167", 3, 1)]
    [InlineData("RAD·031/167", 3, 2)]
    public async Task LeavingASteadfastAuraImmediatelyUpdatesTheGrantedAssault(string unit, int printed, int auras)
    {
        var initial = Position(unit: unit);
        initial = LastBreathDrawBoundaryTests.Add(initial, "TARIC", "OGN·074/298", "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "TARIC", "P1", "BATTLEFIELD", "BF");
        if (auras == 2) {
            initial = LastBreathDrawBoundaryTests.Add(initial, "TARIC2", "OGN·074/298", "P1", "BASE");
            initial = LocalDestructionRecallTests.At(initial, "TARIC2", "P1", "BATTLEFIELD", "BF");
        }
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "RETURN", "OGN·104/298");
        Assert.Equal(printed + auras, Keyword(initial, "ALLY", "坚守"));
        var game = new Driver(initial);
        await game.Act("P1", Activate());
        await game.Top();
        var withAura = Keyword(game.State, "ALLY", "坚守");
        Assert.Equal(printed + auras, withAura);
        Assert.Equal(withAura, Keyword(game.State, "ALLY", "强攻"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["TARIC"]));
        await game.Top();
        Assert.Equal(printed + auras - 1, Keyword(game.State, "ALLY", "坚守"));
        Assert.Equal(printed + auras - 1, Keyword(game.State, "ALLY", "强攻"));
        await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepeatedDynamicGrantsAndExistingAssaultAddIndependently(bool repeated)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with {
                UntilEndOfTurnEffects = repeated ? [Grant + ":1"] : ["COMBAT_KEYWORD:强攻4:1"]
            }
        } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "AXE", "RAD·148/167");
        var game = new Driver(initial);
        await game.Act("P1", Activate());
        await game.Top();
        Assert.Equal(repeated ? 6 : 7, Keyword(game.State, "ALLY", "强攻"));
        if (repeated) Assert.Contains(Grant + ":2", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Act("P1", new PlayCardCommand("AXE", "RAD·148/167", ["ALLY", "ENEMY"]));
        await game.Top();
        Assert.Equal(repeated ? 10 : 9, Keyword(game.State, "ALLY", "强攻"));
        await game.Replay();
    }

    [Fact]
    public async Task KsanteDoublesSteadfastAndTheGrantedAssaultAddsActualAttackingMight()
    {
        var game = new Driver(WithLegend(RadCombatKeywordDoubleTests.Board()));
        await game.Act("P1", Activate("HOST"));
        await game.Top();
        Assert.Equal(1, Keyword(game.State, "HOST", "强攻"));
        await RadCombatKeywordDoubleTests.Pending(game, "HOST");
        await game.Top();
        Assert.Equal(2, Keyword(game.State, "HOST", "坚守"));
        Assert.Equal(2, Keyword(game.State, "HOST", "强攻"));
        Assert.Equal(6, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["HOST"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualRecallBeforeOrAfterResolutionClearsTheGrantAndPreservesPaidCosts(bool beforeResolution)
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298"));
        await game.Act("P1", Activate());
        if (!beforeResolution) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        if (beforeResolution) await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(Grant, StringComparison.Ordinal));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("equipment")]
    [InlineData("enemy")]
    [InlineData("exhausted")]
    [InlineData("mana")]
    [InlineData("wrong-player")]
    public async Task InvalidActivationLeavesTheWholeStateUnchanged(string scenario)
    {
        var state = Position();
        if (scenario == "enemy") state = LocalDestructionRecallTests.At(state, "ALLY", "P2", "BASE");
        if (scenario == "exhausted") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true }
            }
        };
        if (scenario == "mana") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) }
        };
        var command = Activate(scenario == "equipment" ? "GEAR" : "ALLY");
        if (scenario == "missing") command = command with { TargetObjectIds = [] };
        if (scenario == "duplicate") command = command with { TargetObjectIds = ["ALLY", "ALLY"] };
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-nazumah", scenario == "wrong-player" ? "P2" : "P1", CommandTypes.LegendAct), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    [InlineData("cost")]
    [InlineData("timing")]
    public async Task RecoveryRejectsForgedCapturedKeywordActivation(string scenario)
    {
        var game = new Driver(Position());
        await game.Act("P1", Activate());
        var item = Assert.Single(game.State.StackItems);
        var forged = scenario switch {
            "source" => item with { CardNo = "RAD·151/167" },
            "source-generation" => item with { LegendUnitKeyword = item.LegendUnitKeyword! with { SourceGeneration = 99 } },
            "target-generation" => item with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = 99 } },
            "cost" => item with { OptionalCosts = [] },
            _ => item with { TimingContext = TimingStates.SpellDuelOpen }
        };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with { StackItems = [forged] }));
    }
}
