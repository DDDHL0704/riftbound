using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecalledLastDefenderStillAwardsConquestAfterCombat(bool assignedDamage)
    {
        var initial = BuildNaturalStartBattleState(defenderObjectIds: assignedDamage
            ? [BulwarkDefenderObjectId, BackRowDefenderObjectId] : [BulwarkDefenderObjectId]);
        initial = initial with
        {
            Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with { ControllerId = "P2" },
                [BulwarkDefenderObjectId] = initial.CardObjects[BulwarkDefenderObjectId] with
                {
                    UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]
                }
            }
        };
        if (!assignedDamage)
            initial = initial with
            {
                CardObjects = initial.CardObjects.Where(e => e.Key != BackRowDefenderObjectId)
                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
                ObjectLocations = initial.ObjectLocations.Where(e => e.Key != BackRowDefenderObjectId)
                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)
            };
        var driver = ReplacementCombatDriver(initial, $"last-defender-{assignedDamage}");
        var result = await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId,
            [AttackerObjectId], assignedDamage ? [BulwarkDefenderObjectId, BackRowDefenderObjectId]
                : [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        if (assignedDamage)
        {
            await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3)]));
            result = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 1)]));
        }

        Assert.Contains(BulwarkDefenderObjectId, driver.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), BulwarkDefenderObjectId));
        Assert.Contains(AttackerObjectId, driver.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1", driver.State.BattlefieldStates[BattlefieldObjectId].ControllerId);
        Assert.False(driver.State.BattleState.IsActive);
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.True(EventIndex(result.Events, e => e.Kind == "UNIT_RECALLED_TO_BASE")
            < EventIndex(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED"));
        Assert.True(EventIndex(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED")
            < EventIndex(result.Events, e => e.Kind == "BATTLE_CLOSED"));
        await AssertReplacementCombatReplay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecalledAttackerCannotConquerOrHuntFromBase(bool otherAttackerSurvives)
    {
        var initial = SingleDefenderWireState(otherAttackerSurvives);
        initial = initial with
        {
            Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with { ControllerId = "P2" },
                [AttackerObjectId] = initial.CardObjects[AttackerObjectId] with
                {
                    CardNo = "UNL-100/219", Power = 5, Tags = [CardObjectTags.UnitCard, "狩猎3"],
                    UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]
                },
                [BulwarkDefenderObjectId] = initial.CardObjects[BulwarkDefenderObjectId] with { Power = 5 }
            }
        };
        var attackers = otherAttackerSurvives ? new[] { AttackerObjectId, SecondAttackerObjectId } : [AttackerObjectId];
        var driver = ReplacementCombatDriver(initial, $"recalled-attacker-{otherAttackerSurvives}");
        var result = await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, attackers,
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        if (otherAttackerSurvives)
        {
            await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(AttackerObjectId, BulwarkDefenderObjectId, 5), new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2)]));
            result = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(BulwarkDefenderObjectId, AttackerObjectId, 5)]));
        }

        Assert.Contains(AttackerObjectId, driver.State.PlayerZones["P1"].Base);
        Assert.Contains(BulwarkDefenderObjectId, driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(0, driver.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.DoesNotContain(result.Events, e => e.Kind == "EXPERIENCE_GAINED");
        Assert.False(driver.State.BattleState.IsActive);
        Assert.Equal(otherAttackerSurvives ? 1 : 0, driver.State.PlayerScores["P1"]);
        if (otherAttackerSurvives)
        {
            var conquest = Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Equal(SecondAttackerObjectId, conquest.Payload["sourceObjectId"]);
            Assert.Contains(SecondAttackerObjectId, driver.State.PlayerZones["P1"].Battlefields);
        }
        else
        {
            Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Contains(result.Events, e => e.Kind == "BATTLE_NO_RESULT");
            Assert.Null(driver.State.BattlefieldStates[BattlefieldObjectId].ControllerId);
        }
        await AssertReplacementCombatReplay(initial, driver);
    }

    [Theory]
    [InlineData("RECALL")]
    [InlineData("BANISH")]
    public async Task BattleDestructionChoiceCommitsOnceBeforeConquest(string replacement)
    {
        var initial = BuildNaturalStartBattleState() with
        {
            Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }
        };
        initial = initial with
        {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with { ControllerId = "P2" },
                [BulwarkDefenderObjectId] = initial.CardObjects[BulwarkDefenderObjectId] with
                {
                    UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN", "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]
                }
            }
        };
        var driver = ReplacementCombatDriver(initial, $"destruction-choice-{replacement}");
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId],
            [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3)]));
        var before = driver.State;
        var pending = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 1)]));
        var choice = Assert.IsType<RuleChoiceContinuation>(pending.State.PendingRuleChoice);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(choice.Origin));
        Assert.Equal("RULE_CHOICE_REQUESTED", Assert.Single(pending.Events).Kind);
        Assert.Equal(0, pending.State.PlayerScores["P1"]);
        Assert.Equal(0, pending.State.CardObjects[AttackerObjectId].Damage);
        Assert.Empty(pending.State.PlayerZones["P2"].Graveyard);
        var option = $"{replacement}:{BulwarkDefenderObjectId}";
        foreach (var (player, selected) in new[] { ("P1", option), ("P2", "DECLINE"), ("P2", "RECALL:FORGED") })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("invalid-replacement", player, CommandTypes.PayCost),
                new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [selected]), default);
            AssertRejectedNoMutation(driver.State, rejected, ErrorCodes.InvalidTarget);
        }
        var result = await driver.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [option]));

        Assert.Null(result.State.PendingRuleChoice);
        Assert.Equal(4, result.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Single(result.Events, e => e.Kind == "COMBAT_DAMAGE_ASSIGNED");
        Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        Assert.Contains(BulwarkDefenderObjectId, replacement == "RECALL"
            ? driver.State.PlayerZones["P2"].Base : driver.State.PlayerZones["P2"].Banished);
        Assert.Contains(BackRowDefenderObjectId, driver.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), BulwarkDefenderObjectId));
        Assert.False(driver.State.BattleState.IsActive);
        await AssertReplacementCombatReplay(initial, driver);
    }

    private static StandbyDefendObserverTests.Driver ReplacementCombatDriver(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COMBAT_REPLACEMENT_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, scenario));
        driver.Export("initial.json", initial);
        return driver;
    }

    private static async Task AssertReplacementCombatReplay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, JournalRoundTrip(driver.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(driver.State), replay.ReplayedStateHash);
        driver.Export("commands.json", driver.Commands());
        driver.Export("events.json", JournalRoundTrip(driver.Events()));
        driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = driver.Events().Length,
            coldRecoveries = driver.Commands().Length, replay, finalHash = MatchStateHasher.Hash(driver.State),
            engineAssembly = typeof(CoreRuleEngine).Assembly.Location,
            engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Explicit combat fixture; serialized intent, per-command cold restore, duplicate and raw event replay. Not SQL/API or whole-card certification." });
    }
}
