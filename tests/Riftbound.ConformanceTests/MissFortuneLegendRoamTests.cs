using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MissFortuneLegendRoamTests
{
    private static LegendActCommand Grant(params string[] targets)
        => new("LEGEND", LegendActionAbilityCatalog.MissFortuneLegendAbilityId, targets, []);

    [Theory]
    [InlineData("OGN·267/298", false)]
    [InlineData("OGN·267/298", true)]
    [InlineData("OGN·309/298", false)]
    [InlineData("OGN·309/298", true)]
    [InlineData("OGN·309*/298", false)]
    [InlineData("OGN·309*/298", true)]
    public async Task EveryPrintingOffersAndGrantsRoamToEitherPlayersUnit(string printing, bool enemy)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == printing);
        Assert.StartsWith("{{横置}}：让一名单位本回合内获得{{游走}}。", card.CardEffect);
        var initial = LegendAttachmentTests.PositionFor(printing);
        if (enemy) initial = LocalDestructionRecallTests.At(initial, "HOST", "P2", "BASE");
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!,
            c => c.Enabled && c.Action == CommandTypes.LegendAct);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r.GetValueOrDefault("abilityId"), LegendActionAbilityCatalog.MissFortuneLegendAbilityId));
        var targets = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Contains(targets["0"], t => t.Id == "HOST");
        Assert.DoesNotContain(targets["0"], t => t.Id == "GEAR");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", Grant("HOST"));
        Assert.Contains("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(initial.RunePools["P1"], game.State.RunePools["P1"]);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("equipment")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("face-down")]
    [InlineData("hand")]
    [InlineData("exhausted-source")]
    [InlineData("enemy-spellshield")]
    public async Task InvalidTargetsOrUnpaidSpellshieldLeaveTheLegendAndStateUntouched(string boundary)
    {
        var state = LegendAttachmentTests.PositionFor("OGN·267/298");
        if (boundary == "hand")
        {
            state = RadUnitEffectTestFixture.Place(state, "HOST", "P1", "HAND");
            Assert.Contains("HOST", state.PlayerZones["P1"].Hand);
            Assert.DoesNotContain("HOST", state.PlayerZones["P1"].Battlefields);
        }
        if (boundary == "face-down") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = true } } };
        if (boundary == "exhausted-source") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        if (boundary == "enemy-spellshield")
        {
            state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] } },
                RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } };
        }
        var command = Grant(boundary switch { "equipment" => ["GEAR"], "missing" => [],
            "duplicate" => ["HOST", "HOST"], _ => ["HOST"] });
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-roam", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task EnemySpellshieldUsesTheSharedLegendTargetPayment()
    {
        var state = LocalDestructionRecallTests.At(LegendAttachmentTests.PositionFor("OGN·267/298"), "HOST", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 2) } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", Grant("HOST"));
        Assert.Equal(0, game.State.RunePools["P1"].Power);
        Assert.Contains("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }
}
