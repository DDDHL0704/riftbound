using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LanternDragonActivatedAbilityTests
{
    private const string CardNo = "RAD·113/167";
    private const string AbilityId = P4ActivatedAbilityCatalog.LanternDragonAbilityId;

    private static MatchState Position(bool opponentUnit = true)
    {
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF"] = new("BF", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["UNIT"] = new("UNIT", manaCost: 8, power: 8, cardNo: CardNo, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard, "龙", CardObjectTags.Spellshield]),
            ["ENEMY"] = new("ENEMY", manaCost: 2, power: 2, cardNo: "TEST·UNIT", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.UnitCard])
        };
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF"] = new("P1", "BATTLEFIELD", "BF"),
            ["UNIT"] = new("P1", "BATTLEFIELD", "BF"),
            ["ENEMY"] = new("P2", "BASE")
        };
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with { Battlefields = ["BF", "UNIT"] },
            ["P2"] = PlayerZones.Empty with { Base = ["ENEMY"] }
        };
        if (!opponentUnit)
            cards["ENEMY"] = cards["ENEMY"] with { IsFaceDown = true };
        return new MatchState("RAD-LANTERN", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 0), ["P2"] = new(20, 0) }, seed: 42);
    }

    [Fact]
    public void CatalogAndPromptExposeTheOpponentChoice()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(AbilityId, out var ability));
        Assert.Equal(CardNo, ability.SourceCardNo);
        Assert.Equal(1, ability.RequiredTargetCount);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.SimpleEffect!.CommandsOpponentUnitToSourceBattlefield);

        var prompt = ResolutionResult.BuildPrompts(Position())["P1"];
        var candidate = Assert.Single(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            item => Equals(item["abilityId"], AbilityId));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Equal(["P2"], choices["0"].Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public async Task OpponentChoosesAUnitAndMovesItToLanternsBattlefield()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("UNIT", AbilityId, ["P2"]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await ResolveToChoice(game);
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P2", choice.Request.PlayerId);
        Assert.Equal(["MOVE:ENEMY"], choice.Request.Options.Select(option => option.Id).ToArray());
        await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["MOVE:ENEMY"]));
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Contains(game.Events(), item => item.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(item.Event.Payload["movingPlayerId"], "P2"));
        await game.Replay();
    }

    [Fact]
    public async Task NoLegalOpponentUnitResolvesWithoutMovingAnything()
    {
        var game = new Driver(Position(opponentUnit: false));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", AbilityId, ["P2"]));
        await ResolveToChoice(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Empty(game.State.StackItems);
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT");
        await game.Replay();
    }

    [Fact]
    public async Task NonOpponentTargetIsRejectedWithoutExhaustingTheSource()
    {
        var state = Position();
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("lantern-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("UNIT", AbilityId, ["P1"]), default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.False(result.State.CardObjects["UNIT"].IsExhausted);
    }

    private static async Task ResolveToChoice(Driver game)
    {
        await game.Top();
    }
}
