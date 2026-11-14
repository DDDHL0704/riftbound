using System.Text;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN 467.2.a-c, c.4.a/c.5; VEN FAQ's finite prevention + Lotus example.
public sealed class CombatOriginalDamageBudgetTests
{
    private const string Double = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private const string Order = "BATTLE_DAMAGE_REPLACEMENT_ORDER:";

    [Theory]
    [InlineData(3, 0, 1, 2)] [InlineData(3, 1, 1, 1)]
    [InlineData(5, 0, 1, 3)] [InlineData(5, 0, 2, 2)]
    [InlineData(9, 0, 3, 2)] [InlineData(1, 0, 3, 1)]
    [InlineData(0, 0, 1, 1)] [InlineData(7, 2, 2, 2)]
    public void PromptUsesTheSmallestOriginalAmountRatherThanTheDoubledMarkedAmount(int power, int marked, int doubles, int expected)
    {
        var state = PooledPosition(power, marked, doubles);
        var before = MatchStateHasher.Hash(state);
        var metadata = ResolutionResult.BuildPrompts(state)["P1"].View!.Metadata!;
        Assert.Equal(expected, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["lethalDamageThreshold"])["D1"]);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public async Task IndependentSourcesShareOneOriginalBudgetAndTheFinalEventIsNotDoubledAgain()
    {
        var state = PooledPosition(5, 0, 2);
        var engine = new CoreRuleEngine();
        var valid = new AssignCombatDamageCommand("battle:BF", "BF",
            [new("A1", "D1", 1), new("A1", "D2", 1), new("A2", "D1", 1), new("A2", "D2", 1)]);
        var invalid = valid with { Assignments = [new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1)] };
        await Reject(state, "P1", invalid);
        var first = await engine.ResolveAsync(state, new("raw-pool-first", "P1", CommandTypes.AssignCombatDamage), valid, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        Assert.All(first.State.CardObjects.Values, c => Assert.Equal(0, c.Damage));
        var second = await engine.ResolveAsync(first.State, new("raw-pool-second", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", [new("D1", "A1", 2), new("D1", "A2", 3), new("D2", "A2", 4)]), default);
        Assert.True(second.Accepted, second.ErrorMessage);
        Assert.Equal(8, Damage(second.Events, "D1"));
        Assert.Equal(2, Damage(second.Events, "D2"));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task RealLotusPlaysAndNormalBattleCommandsKeepTheOriginalPool(int doubles)
    {
        var game = await Opening(doubles, shield: false);
        var minimum = doubles == 1 ? 2 : 1;
        Assert.Equal(minimum, Threshold(game.State, "ENEMY"));
        await game.Act("P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER",
            doubles == 1 ? [new("ALLY", "ENEMY", 1), new("ALLY", "ENEMY2", 3), new("A2", "ENEMY", 1), new("A2", "ENEMY2", 3)]
                : [new("ALLY", "ENEMY", 1), new("ALLY", "ENEMY2", 3), new("A2", "ENEMY2", 4)]));
        await Defender(game);
        Assert.Equal(4, Damage(game.Events().Select(e => e.Event), "ENEMY"));
        Assert.Equal(doubles == 1 ? 6 : 7, Damage(game.Events().Select(e => e.Event), "ENEMY2"));
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TheRecipientChoosesReplacementOrderBeforeAllocationAndTheCapturedOrderSurvivesRecovery(bool doubleFirst)
    {
        var game = await Opening(1, shield: true);
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P2", pending.Request.PlayerId); Assert.Equal("DAMAGE_REPLACEMENT_ORDER", pending.Request.Kind);
        Assert.Equal(2, pending.Request.Options.Count);
        await Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["DOUBLE"]));
        var selected = doubleFirst ? "DOUBLE" : pending.Request.Options.Single(o => o.Id.StartsWith("DAMAGE_PREVENTION_THIS_TURN:")).Id;
        await game.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selected]));
        Assert.Equal(doubleFirst ? 6 : 9, Threshold(game.State, "ENEMY"));
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Order));
        foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values)
            Assert.DoesNotContain(Order, JsonSerializer.Serialize(snapshot));
        var forged = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(marker[Order.Length..]));
        var encodedOrder = Encoding.UTF8.GetString(Convert.FromBase64String(marker[Order.Length..]));
        foreach (var replacement in new[] { "\"DOUBLE:9\"", "null" })
        {
            var badOrder = encodedOrder.Replace("\"DOUBLE\"", replacement, StringComparison.Ordinal);
            var bad = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => e != marker)
                .Append(Order + Convert.ToBase64String(Encoding.UTF8.GetBytes(badOrder))).Order(StringComparer.Ordinal).ToArray() };
            Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(bad), e => e.Contains("invalid combat damage replacement order"));
            await Reject(bad, "P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER", [new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 4)]));
        }
        Assert.True(forged.GetProperty("Targets").GetArrayLength() > 0);
        await game.Act("P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER",
            doubleFirst ? [new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 2), new("A2", "ENEMY2", 2)]
                : [new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 4)]));
        await Defender(game);
        Assert.Equal(doubleFirst ? 5 : 2, Damage(game.Events().Select(e => e.Event), "ENEMY"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Order));
        await game.Replay();
    }

    [Fact]
    public async Task SurrenderClearsTheCapturedBattleOrder()
    {
        var game = await Opening(1, shield: true);
        var p = game.State.PendingRuleChoice!;
        await game.Act("P2", new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", ["DOUBLE"]));
        await game.Act("P1", new SurrenderCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Order));
        await game.Replay();
    }

    [Fact]
    public async Task IdenticalFiniteShieldsKeepTheChosenSourceAndOneBudgetAcrossAllCombatContributors()
    {
        var game = await Opening(0, shield: true, shieldCopies: 2);
        var choice = game.State.PendingRuleChoice!;
        Assert.Equal("P2", choice.Request.PlayerId);
        var chosen = choice.Request.Options[1].Id;
        await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [chosen]));
        Assert.Equal(18, Threshold(game.State, "ENEMY"));
        await game.Act("P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER", [new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 4)]));
        await Defender(game);
        Assert.Equal(0, Damage(game.Events().Select(e => e.Event), "ENEMY"));
        Assert.DoesNotContain(game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects, e => e.StartsWith(chosen + ":"));
        Assert.Contains(game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_PREVENTION_THIS_TURN:") && e.EndsWith(":6"));
        await game.Replay();
    }

    private static MatchState PooledPosition(int power, int marked, int doubles)
    {
        var state = OfficialPooledCombatDamageTests.Position(defenderOnePower: power, defenderTwoPower: 4, bulwark: true);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D1"] = state.CardObjects["D1"] with { Damage = marked,
                UntilEndOfTurnEffects = Enumerable.Range(1, doubles).Select(i => i == 1 ? Double : Double + ":" + i).Order(StringComparer.Ordinal).ToArray() } } };
    }

    private static int Threshold(MatchState state, string id)
        => Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(ResolutionResult.BuildPrompts(state)["P1"].View!.Metadata!["lethalDamageThreshold"])[id];
    private static int Damage(IEnumerable<GameEvent> events, string target)
        => events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), target))
            .Sum(e => Convert.ToInt32(e.Payload["damage"]));
    private static async Task<Driver> Opening(int doubles, bool shield, int shieldCopies = 1)
    {
        var state = Put(Create("OGN·297/298"), "OTHER", "OGN·297/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = Put(Put(state, "A2", "OGN·219/298", "P1", "BASE"), "ENEMY2", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        for (var i = 1; i <= doubles; i++) state = Put(state, "LOTUS" + i, "UNL-013/219", "P1", "HAND");
        if (shield) for (var i = 0; i < shieldCopies; i++) state = Put(state, "SHIELD" + i, "VEN·126", "P1", "HAND");
        var game = new Driver(state);
        if (shield) for (var i = 0; i < shieldCopies; i++) { await game.Act("P1", new PlayCardCommand("SHIELD" + i, "VEN·126", ["ENEMY"])); await game.Top(); }
        for (var i = 1; i <= doubles; i++) { await game.Act("P1", new PlayCardCommand("LOTUS" + i, "UNL-013/219", ["ENEMY"])); await game.Top(); }
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], ["ALLY", "A2"]));
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY", "A2"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        return game;
    }
    private static Task Defender(Driver game) => game.Act("P2", new AssignCombatDamageCommand("battle:OTHER", "OTHER",
        [new("ENEMY", "ALLY", 4), new("ENEMY2", "A2", 4)]));
}
