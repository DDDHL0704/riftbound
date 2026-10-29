using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P4BattleCleanupRecallsTemporarilyControlledAttackerToOwnerBase()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-OWNED-CONTROLLED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-OWNED-CONTROLLED-ATTACKER"] = new(
                    "P2-OWNED-CONTROLLED-ATTACKER",
                    untilEndOfTurnEffects: ["STUNNED"],
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-battle-cleanup-recall-owner-base", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P2-OWNED-CONTROLLED-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var recalledEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal("P2-OWNED-CONTROLLED-ATTACKER", recalledEvent.Payload["targetObjectId"]);
        Assert.Equal("P2", recalledEvent.Payload["ownerPlayerId"]);
        Assert.Equal("P1", recalledEvent.Payload["controllerId"]);
        Assert.Equal("BATTLE_CLEANUP_ATTACKER_RECALL", recalledEvent.Payload["reason"]);

        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-OWNED-CONTROLLED-ATTACKER"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        var attackerState = result.State.CardObjects["P2-OWNED-CONTROLLED-ATTACKER"];
        Assert.Equal("P2", attackerState.OwnerId);
        Assert.Equal("P1", attackerState.ControllerId);
        Assert.Equal(0, attackerState.Damage);
        Assert.False(attackerState.IsAttacking);
        var attackerLocation = result.State.ObjectLocations["P2-OWNED-CONTROLLED-ATTACKER"];
        Assert.Equal("P2", attackerLocation.PlayerId);
        Assert.Equal("BASE", attackerLocation.Zone);
        Assert.Null(attackerLocation.BattlefieldObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public void ActionPromptDeclareBattleIncludesExhaustedAttackersAndDefenders()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-READY-ATTACKER", "P1-BATTLEFIELD-EXHAUSTED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-READY-DEFENDER", "P2-BATTLEFIELD-EXHAUSTED-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-READY-ATTACKER",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EXHAUSTED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EXHAUSTED-ATTACKER",
                    isExhausted: true,
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EXHAUSTED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EXHAUSTED-DEFENDER",
                    isExhausted: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var declareBattleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

        Assert.True(declareBattleCandidate.Enabled);
        Assert.NotNull(declareBattleCandidate.Sources);
        Assert.NotNull(declareBattleCandidate.Targets);
        Assert.Equal(
            ["P1-BATTLEFIELD-READY-ATTACKER", "P1-BATTLEFIELD-EXHAUSTED-ATTACKER"],
            declareBattleCandidate.Sources.Select(choice => choice.Id).ToArray());
        Assert.Equal(
            ["P2-BATTLEFIELD-READY-DEFENDER", "P2-BATTLEFIELD-EXHAUSTED-DEFENDER"],
            declareBattleCandidate.Targets.Select(choice => choice.Id).ToArray());
        Assert.Equal(
            "battlefield-zone-controlled-face-up-units-not-already-in-combat",
            declareBattleCandidate.Metadata?["candidateFiltering"]);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAcceptsExhaustedAttackersAndDefenders()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-READY-ATTACKER", "P1-BATTLEFIELD-EXHAUSTED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-READY-DEFENDER", "P2-BATTLEFIELD-EXHAUSTED-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-READY-ATTACKER",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EXHAUSTED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EXHAUSTED-ATTACKER",
                    isExhausted: true,
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EXHAUSTED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EXHAUSTED-DEFENDER",
                    isExhausted: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var exhaustedAttackerResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-exhausted-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-EXHAUSTED-ATTACKER"],
                ["P2-BATTLEFIELD-READY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(exhaustedAttackerResult.Accepted, exhaustedAttackerResult.ErrorMessage);
        Assert.Contains(exhaustedAttackerResult.Events, e => e.Kind == "BATTLE_DECLARED");
        Assert.True(exhaustedAttackerResult.State.Tick > state.Tick);

        var exhaustedDefenderResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-exhausted-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-READY-ATTACKER"],
                ["P2-BATTLEFIELD-EXHAUSTED-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(exhaustedDefenderResult.Accepted, exhaustedDefenderResult.ErrorMessage);
        Assert.Contains(exhaustedDefenderResult.Events, e => e.Kind == "BATTLE_DECLARED");
        Assert.True(exhaustedDefenderResult.State.Tick > state.Tick);
    }

    [Fact]
    public async Task P4DeclareBattleCommandGrantsHuntExperienceWhenAttackerConquersBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"] = new(
                    "P1-BATTLEFIELD-GLUTTONOUS-TOADFROG",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "狩猎3"],
                    cardNo: "UNL-100/219"),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-hunt-conquest-experience", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Collection(
            result.Events,
            battleEvent =>
            {
                Assert.Equal("BATTLE_DECLARED", battleEvent.Kind);
                Assert.Equal("P1", battleEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", battleEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"], Assert.IsType<string[]>(battleEvent.Payload["attackerObjectIds"]));
                Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], Assert.IsType<string[]>(battleEvent.Payload["defenderObjectIds"]));
                Assert.Equal(["COMBAT_ASSIGNMENT"], Assert.IsType<string[]>(battleEvent.Payload["optionalCosts"]));
            },
            attackerDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", attackerDamageEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-GLUTTONOUS-TOADFROG", attackerDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", attackerDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("ATTACKER", attackerDamageEvent.Payload["combatRole"]);
                Assert.Equal("强攻", attackerDamageEvent.Payload["keyword"]);
                Assert.Equal(5, attackerDamageEvent.Payload["basePower"]);
                Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(5, attackerDamageEvent.Payload["combatPower"]);
                Assert.Equal(5, attackerDamageEvent.Payload["damage"]);
            },
            defenderDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", defenderDamageEvent.Kind);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", defenderDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P1-BATTLEFIELD-GLUTTONOUS-TOADFROG", defenderDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("DEFENDER", defenderDamageEvent.Payload["combatRole"]);
                Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
                Assert.Equal(1, defenderDamageEvent.Payload["basePower"]);
                Assert.Equal(2, defenderDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
                Assert.Equal(3, defenderDamageEvent.Payload["damage"]);
            },
            destroyedEvent =>
            {
                Assert.Equal("UNIT_DESTROYED", destroyedEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-GLUTTONOUS-TOADFROG", destroyedEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", destroyedEvent.Payload["targetObjectId"]);
                Assert.Equal("P2", destroyedEvent.Payload["ownerPlayerId"]);
                Assert.Equal("P1", destroyedEvent.Payload["destroyedByPlayerId"]);
                Assert.Equal("GRAVEYARD", destroyedEvent.Payload["destinationZone"]);
                Assert.Equal("LETHAL_DAMAGE", destroyedEvent.Payload["reason"]);
            },
            damageRemovedEvent =>
            {
                Assert.Equal("DAMAGE_REMOVED", damageRemovedEvent.Kind);
                Assert.Equal("BATTLEFIELD:P1-MAIN", damageRemovedEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
                Assert.Equal(1, damageRemovedEvent.Payload["count"]);
                var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
                Assert.Equal(3, previousDamageByObject["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"]);
                Assert.Equal(3, damageRemovedEvent.Payload["totalDamageRemoved"]);
                Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);
            },
            cleanupEvent =>
            {
                Assert.Equal("BATTLE_CLEANUP_COMPLETED", cleanupEvent.Kind);
                var cleanup = Assert.IsType<BattleResultContinuation>(cleanupEvent.Payload["continuation"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", cleanup.BattlefieldId);
                Assert.Equal("CLEANUP_TRIGGERS", cleanup.Stage);
                Assert.Null(cleanup.WinnerPlayerId); Assert.False(cleanup.AttackerRecalledByBattleCleanup);
            },
            resultEvent =>
            {
                Assert.Equal("BATTLE_RESULT_DETERMINED", resultEvent.Kind);
                Assert.Equal("P1", Assert.IsType<BattleResultContinuation>(resultEvent.Payload["continuation"]).WinnerPlayerId);
            },
            controlEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONTROL_RESOLVED", controlEvent.Kind);
                Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
                Assert.Equal("P1", controlEvent.Payload["controllerId"]);
            },
            conqueredEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONQUERED", conqueredEvent.Kind);
                Assert.Equal("P1", conqueredEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", conqueredEvent.Payload["battlefieldId"]);
                Assert.Equal("P1-BATTLEFIELD-GLUTTONOUS-TOADFROG", conqueredEvent.Payload["sourceObjectId"]);
                Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], Assert.IsType<string[]>(conqueredEvent.Payload["defeatedObjectIds"]));
                Assert.Equal(3, conqueredEvent.Payload["huntAmount"]);
            },
            scoreEvent =>
            {
                Assert.Equal("SCORE_GAINED", scoreEvent.Kind);
                Assert.Equal("P1", scoreEvent.Payload["playerId"]);
            },
            experienceEvent =>
            {
                Assert.Equal("EXPERIENCE_GAINED", experienceEvent.Kind);
                Assert.Equal("P1", experienceEvent.Payload["playerId"]);
                Assert.Equal("P1-BATTLEFIELD-GLUTTONOUS-TOADFROG", experienceEvent.Payload["sourceObjectId"]);
                Assert.Equal("UNL-100/219", experienceEvent.Payload["cardNo"]);
                Assert.Equal(3, experienceEvent.Payload["amount"]);
                Assert.Equal(3, experienceEvent.Payload["totalExperience"]);
            },
            closedEvent =>
            {
                Assert.Equal("BATTLE_CLOSED", closedEvent.Kind);
            });
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.Equal(0, result.State.PlayerExperience["P2"]);
        Assert.Equal(["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Graveyard);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"].IsAttacking);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"].Damage);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"].Power);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandGrantsHuntExperienceWhenDefenderHoldsBattlefieldDoesNotTriggerOnDefensiveVictory()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-HUNTER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-HUNTER"] = new(
                    "P2-BATTLEFIELD-HUNTER",
                    cardNo: "UNL-059/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "狩猎2"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-hunt-held-experience", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-HUNTER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAssignsDamageToBulwarkBeforeBackRowForRepresentativePath()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-VOLIBEAR"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VOLIBEAR"] = new(
                    "P1-BATTLEFIELD-VOLIBEAR",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "坚守3", "壁垒"],
                    cardNo: "OGN·158/298"),
                ["P2-BATTLEFIELD-LEBLANC"] = new(
                    "P2-BATTLEFIELD-LEBLANC",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "后排"],
                    cardNo: "UNL-090/219"),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-bulwark-back-row", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-VOLIBEAR"],
                ["P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        result = await P4FinishBattleAssignments(result, [new("P1-BATTLEFIELD-VOLIBEAR", "P2-BATTLEFIELD-MUTANT-KITTEN", 3),
                new("P1-BATTLEFIELD-VOLIBEAR", "P2-BATTLEFIELD-LEBLANC", 7)],
            [new("P2-BATTLEFIELD-MUTANT-KITTEN", "P1-BATTLEFIELD-VOLIBEAR", 3),
                new("P2-BATTLEFIELD-LEBLANC", "P1-BATTLEFIELD-VOLIBEAR", 4)]);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Collection(
            result.Events.Where(e => e.Kind is not ("BATTLE_DAMAGE_ASSIGNMENT_OPENED"
                or "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED" or "BATTLE_DAMAGE_STEP_STARTED" or "COMBAT_DAMAGE_ASSIGNED")),
            battleEvent =>
            {
                Assert.Equal("BATTLE_DECLARED", battleEvent.Kind);
                Assert.Equal("P1", battleEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", battleEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], Assert.IsType<string[]>(battleEvent.Payload["attackerObjectIds"]));
                Assert.Equal(
                    ["P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"],
                    Assert.IsType<string[]>(battleEvent.Payload["defenderObjectIds"]));
                Assert.Equal(["COMBAT_ASSIGNMENT"], Assert.IsType<string[]>(battleEvent.Payload["optionalCosts"]));
            },
            bulwarkDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", bulwarkDamageEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", bulwarkDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", bulwarkDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("ATTACKER", bulwarkDamageEvent.Payload["combatRole"]);
                Assert.Equal("BULWARK_FIRST", bulwarkDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(1, bulwarkDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(10, bulwarkDamageEvent.Payload["basePower"]);
                Assert.Equal(0, bulwarkDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(10, bulwarkDamageEvent.Payload["combatPower"]);
                Assert.Equal(3, bulwarkDamageEvent.Payload["damage"]);
            },
            backRowDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", backRowDamageEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", backRowDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-LEBLANC", backRowDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("ATTACKER", backRowDamageEvent.Payload["combatRole"]);
                Assert.Equal("BACK_ROW_LAST", backRowDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(2, backRowDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(10, backRowDamageEvent.Payload["basePower"]);
                Assert.Equal(0, backRowDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(10, backRowDamageEvent.Payload["combatPower"]);
                Assert.Equal(7, backRowDamageEvent.Payload["damage"]);
            },
            bulwarkDefenderDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", bulwarkDefenderDamageEvent.Kind);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", bulwarkDefenderDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", bulwarkDefenderDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("DEFENDER", bulwarkDefenderDamageEvent.Payload["combatRole"]);
                Assert.Equal(1, bulwarkDefenderDamageEvent.Payload["basePower"]);
                Assert.Equal(2, bulwarkDefenderDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(3, bulwarkDefenderDamageEvent.Payload["combatPower"]);
                Assert.Equal(3, bulwarkDefenderDamageEvent.Payload["damage"]);
            },
            backRowDefenderDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", backRowDefenderDamageEvent.Kind);
                Assert.Equal("P2-BATTLEFIELD-LEBLANC", backRowDefenderDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", backRowDefenderDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("DEFENDER", backRowDefenderDamageEvent.Payload["combatRole"]);
                Assert.Equal(4, backRowDefenderDamageEvent.Payload["basePower"]);
                Assert.Equal(0, backRowDefenderDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(4, backRowDefenderDamageEvent.Payload["combatPower"]);
                Assert.Equal(4, backRowDefenderDamageEvent.Payload["damage"]);
            },
            leblancDestroyedEvent =>
            {
                Assert.Equal("UNIT_DESTROYED", leblancDestroyedEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", leblancDestroyedEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-LEBLANC", leblancDestroyedEvent.Payload["targetObjectId"]);
                Assert.Equal("P2", leblancDestroyedEvent.Payload["ownerPlayerId"]);
                Assert.Equal("P1", leblancDestroyedEvent.Payload["destroyedByPlayerId"]);
                Assert.Equal("GRAVEYARD", leblancDestroyedEvent.Payload["destinationZone"]);
                Assert.Equal("LETHAL_DAMAGE", leblancDestroyedEvent.Payload["reason"]);
            },
            mutantKittenDestroyedEvent =>
            {
                Assert.Equal("UNIT_DESTROYED", mutantKittenDestroyedEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-VOLIBEAR", mutantKittenDestroyedEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", mutantKittenDestroyedEvent.Payload["targetObjectId"]);
                Assert.Equal("P2", mutantKittenDestroyedEvent.Payload["ownerPlayerId"]);
                Assert.Equal("P1", mutantKittenDestroyedEvent.Payload["destroyedByPlayerId"]);
                Assert.Equal("GRAVEYARD", mutantKittenDestroyedEvent.Payload["destinationZone"]);
                Assert.Equal("LETHAL_DAMAGE", mutantKittenDestroyedEvent.Payload["reason"]);
            },
            damageRemovedEvent =>
            {
                Assert.Equal("DAMAGE_REMOVED", damageRemovedEvent.Kind);
                Assert.Equal("BATTLEFIELD:P1-MAIN", damageRemovedEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
                Assert.Equal(1, damageRemovedEvent.Payload["count"]);
                var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
                Assert.Equal(7, previousDamageByObject["P1-BATTLEFIELD-VOLIBEAR"]);
                Assert.Equal(7, damageRemovedEvent.Payload["totalDamageRemoved"]);
                Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);
            },
            cleanupEvent =>
            {
                Assert.Equal("BATTLE_CLEANUP_COMPLETED", cleanupEvent.Kind);
                var cleanup = Assert.IsType<BattleResultContinuation>(cleanupEvent.Payload["continuation"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", cleanup.BattlefieldId);
                Assert.Equal("CLEANUP_TRIGGERS", cleanup.Stage);
                Assert.Null(cleanup.WinnerPlayerId); Assert.False(cleanup.AttackerRecalledByBattleCleanup);
            },
            resultEvent =>
            {
                Assert.Equal("BATTLE_RESULT_DETERMINED", resultEvent.Kind);
                Assert.Equal("P1", Assert.IsType<BattleResultContinuation>(resultEvent.Payload["continuation"]).WinnerPlayerId);
            },
            controlEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONTROL_RESOLVED", controlEvent.Kind);
                Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
                Assert.Equal("P1", controlEvent.Payload["controllerId"]);
            },
            conquestEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONQUERED", conquestEvent.Kind);
                Assert.Equal("P1", conquestEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", conquestEvent.Payload["battlefieldId"]);
            },
            scoreEvent =>
            {
                Assert.Equal("SCORE_GAINED", scoreEvent.Kind);
                Assert.Equal("P1", scoreEvent.Payload["playerId"]);
            },
            closedEvent =>
            {
                Assert.Equal("BATTLE_CLOSED", closedEvent.Kind);
                Assert.Equal("BATTLEFIELD:P1-MAIN", closedEvent.Payload["battlefieldId"]);
                Assert.Equal(
                    ["P1-BATTLEFIELD-VOLIBEAR", "P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"],
                    Assert.IsType<string[]>(closedEvent.Payload["participantObjectIds"]));
                Assert.Equal(
                    ["P1-BATTLEFIELD-VOLIBEAR"],
                    Assert.IsType<string[]>(closedEvent.Payload["clearedObjectIds"]));
            });
        Assert.Equal(["BATTLE_DAMAGE_ASSIGNMENT_OPENED", "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED",
            "BATTLE_DAMAGE_STEP_STARTED", "COMBAT_DAMAGE_ASSIGNED"], result.Events
            .Where(e => e.Kind is "BATTLE_DAMAGE_ASSIGNMENT_OPENED" or "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED"
                or "BATTLE_DAMAGE_STEP_STARTED" or "COMBAT_DAMAGE_ASSIGNED").Select(e => e.Kind));
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Graveyard);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-VOLIBEAR"].IsAttacking);
        Assert.False(result.State.BattleState.IsActive);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-VOLIBEAR"].Damage);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-LEBLANC", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandPreservesSubmittedOrderForSamePriorityBulwarkDefenders()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-VOLIBEAR"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BULWARK-A", "P2-BATTLEFIELD-BULWARK-B"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VOLIBEAR"] = new(
                    "P1-BATTLEFIELD-VOLIBEAR",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    cardNo: "OGN·158/298"),
                ["P2-BATTLEFIELD-BULWARK-A"] = new(
                    "P2-BATTLEFIELD-BULWARK-A",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    cardNo: "UNL-036/219"),
                ["P2-BATTLEFIELD-BULWARK-B"] = new(
                    "P2-BATTLEFIELD-BULWARK-B",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    cardNo: "UNL-036/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-same-priority-bulwark", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-VOLIBEAR"],
                ["P2-BATTLEFIELD-BULWARK-B", "P2-BATTLEFIELD-BULWARK-A"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var attackerDamageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-VOLIBEAR", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            ["P2-BATTLEFIELD-BULWARK-B", "P2-BATTLEFIELD-BULWARK-A"],
            attackerDamageEvents.Select(gameEvent => (string)gameEvent.Payload["targetObjectId"]!).ToArray());
        Assert.Collection(
            attackerDamageEvents,
            firstBulwarkDamageEvent =>
            {
                Assert.Equal("P2-BATTLEFIELD-BULWARK-B", firstBulwarkDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("BULWARK_FIRST", firstBulwarkDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(1, firstBulwarkDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(4, firstBulwarkDamageEvent.Payload["damage"]);
            },
            secondBulwarkDamageEvent =>
            {
                Assert.Equal("P2-BATTLEFIELD-BULWARK-A", secondBulwarkDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("BULWARK_FIRST", secondBulwarkDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(2, secondBulwarkDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(6, secondBulwarkDamageEvent.Payload["damage"]);
            });
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        Assert.Equal(1, result.State.Tick);
        Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-BATTLEFIELD-BULWARK-A", result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-BATTLEFIELD-BULWARK-B", result.State.PlayerZones["P2"].Graveyard);
        var damageRemovedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
        var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
        Assert.Equal(8, previousDamageByObject["P1-BATTLEFIELD-VOLIBEAR"]);
        Assert.Equal(8, damageRemovedEvent.Payload["totalDamageRemoved"]);
        Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-VOLIBEAR"].Damage);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-VOLIBEAR"].IsAttacking);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-BULWARK-A", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-BULWARK-B", "GRAVEYARD");
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-BATTLEFIELD-YI"] = new(
                    "P1-BATTLEFIELD-YI",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-059/219"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-036/219")
            }
        };
        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-multi-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        result = await P4FinishBattleAssignments(result, [new("P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-DEFENDER", 5), new("P1-BATTLEFIELD-YI", "P2-BATTLEFIELD-DEFENDER", 2)],
            [new("P2-BATTLEFIELD-DEFENDER", "P1-BATTLEFIELD-GAREN", 5), new("P2-BATTLEFIELD-DEFENDER", "P1-BATTLEFIELD-YI", 1)]);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var battleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"],
            Assert.IsType<string[]>(battleEvent.Payload["attackerObjectIds"]));
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], Assert.IsType<string[]>(battleEvent.Payload["defenderObjectIds"]));

        var damageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 5));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-YI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 5));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-YI", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 1));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        Assert.Equal(3, result.State.Tick);
        Assert.Equal(["P1-BATTLEFIELD-YI"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P1-BATTLEFIELD-GAREN", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-DEFENDER", "GRAVEYARD");
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-YI"].IsAttacking);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-YI"].Damage);
        Assert.Equal(["P1", "P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Contains("BATTLEFIELD:P1-MAIN", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        var battleResolution = Assert.Single(result.State.BattleResolutions);
        Assert.Equal(result.State.Tick, battleResolution.Tick);
        Assert.Equal("CLOSED", battleResolution.Kind);
        Assert.Equal("BATTLEFIELD:P1-MAIN", battleResolution.BattlefieldId);
        Assert.Equal("P1", battleResolution.AttackingPlayerId);
        Assert.Equal("P2", battleResolution.DefendingPlayerId);
        Assert.Equal("P1", battleResolution.WinnerPlayerId);
        Assert.Equal(["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"], battleResolution.AttackerObjectIds);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], battleResolution.DefenderObjectIds);
        Assert.Equal(["P1-BATTLEFIELD-YI"], battleResolution.SurvivingAttackerObjectIds);
        Assert.Empty(battleResolution.SurvivingDefenderObjectIds);
        Assert.Equal(
            ["P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-DEFENDER"],
            battleResolution.DestroyedObjectIds);
        Assert.Contains("DAMAGE_REMOVED", battleResolution.RelatedEventKinds);
        var snapshotBattleResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["battleResolutions"]);
        var snapshotBattleResolution = Assert.Single(snapshotBattleResolutions);
        Assert.Equal("CLOSED", snapshotBattleResolution["kind"]);
        Assert.Equal("P1", snapshotBattleResolution["winnerPlayerId"]);
        var snapshotRelatedEventKinds = Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotBattleResolution["relatedEventKinds"]);
        Assert.Contains("DAMAGE_REMOVED", snapshotRelatedEventKinds);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandGrantsHuntExperienceFromSurvivingSecondAttacker()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-VANGUARD", "P1-BATTLEFIELD-HUNTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VANGUARD"] = new(
                    "P1-BATTLEFIELD-VANGUARD",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-HUNTER"] = new(
                    "P1-BATTLEFIELD-HUNTER",
                    cardNo: "UNL-059/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "狩猎2"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-second-hunter", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-VANGUARD", "P1-BATTLEFIELD-HUNTER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        result = await P4FinishBattleAssignments(result, [new("P1-BATTLEFIELD-VANGUARD", "P2-BATTLEFIELD-DEFENDER", 1),
                new("P1-BATTLEFIELD-HUNTER", "P2-BATTLEFIELD-DEFENDER", 2)],
            [new("P2-BATTLEFIELD-DEFENDER", "P1-BATTLEFIELD-VANGUARD", 1)]);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var conqueredEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Equal("P1", conqueredEvent.Payload["playerId"]);
        Assert.Equal("P1-BATTLEFIELD-HUNTER", conqueredEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, conqueredEvent.Payload["huntAmount"]);
        Assert.Equal(["P1-BATTLEFIELD-HUNTER"], Assert.IsType<string[]>(conqueredEvent.Payload["huntSourceObjectIds"]));
        var huntAmountsBySource = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(conqueredEvent.Payload["huntAmountsBySource"]);
        Assert.Equal(2, huntAmountsBySource["P1-BATTLEFIELD-HUNTER"]);
        var experienceEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-HUNTER", experienceEvent.Payload["sourceObjectId"]);
        Assert.Equal("UNL-059/219", experienceEvent.Payload["cardNo"]);
        Assert.Equal(2, experienceEvent.Payload["amount"]);
        Assert.Equal(2, experienceEvent.Payload["totalExperience"]);

        Assert.Equal(2, result.State.PlayerExperience["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-HUNTER"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Equal(["P1-BATTLEFIELD-VANGUARD"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P1-BATTLEFIELD-VANGUARD", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-DEFENDER", "GRAVEYARD");
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-HUNTER"].IsAttacking);
        Assert.Equal(["P1", "P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAssignsDamageForMultiAttackerMultiDefenderRepresentativePath()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "OGS·007/024"),
                ["P1-BATTLEFIELD-YI"] = new(
                    "P1-BATTLEFIELD-YI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-059/219"),
                ["P2-BATTLEFIELD-BULWARK"] = new(
                    "P2-BATTLEFIELD-BULWARK",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    cardNo: "UNL-036/219"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-036/219")
            }
        };
        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-multi-participant", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"],
                ["P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        result = await P4FinishBattleAssignments(result, [new("P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-BULWARK", 4), new("P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-DEFENDER", 2),
                new("P1-BATTLEFIELD-YI", "P2-BATTLEFIELD-DEFENDER", 3)],
            [new("P2-BATTLEFIELD-BULWARK", "P1-BATTLEFIELD-GAREN", 4), new("P2-BATTLEFIELD-DEFENDER", "P1-BATTLEFIELD-GAREN", 2),
                new("P2-BATTLEFIELD-DEFENDER", "P1-BATTLEFIELD-YI", 1)]);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var battleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"],
            Assert.IsType<string[]>(battleEvent.Payload["attackerObjectIds"]));
        Assert.Equal(
            ["P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"],
            Assert.IsType<string[]>(battleEvent.Payload["defenderObjectIds"]));

        var damageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BULWARK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "BULWARK_FIRST", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 1)
            && Equals(gameEvent.Payload["damage"], 4));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "NORMAL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 2)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-YI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "NORMAL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 2)
            && Equals(gameEvent.Payload["damage"], 3));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-BULWARK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "NORMAL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 1)
            && Equals(gameEvent.Payload["damage"], 4));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "NORMAL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 1)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-YI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "NORMAL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 2)
            && Equals(gameEvent.Payload["damage"], 1));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        Assert.Equal(3, result.State.Tick);
        Assert.Equal(["P1-BATTLEFIELD-YI"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P1-BATTLEFIELD-GAREN", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-BULWARK", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-DEFENDER", "GRAVEYARD");
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-YI"].IsAttacking);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-YI"].Damage);
        Assert.Equal(["P1", "P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Contains("BATTLEFIELD:P1-MAIN", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        var battleResolution = Assert.Single(result.State.BattleResolutions);
        Assert.Equal(result.State.Tick, battleResolution.Tick);
        Assert.Equal("CLOSED", battleResolution.Kind);
        Assert.Equal("BATTLEFIELD:P1-MAIN", battleResolution.BattlefieldId);
        Assert.Equal("P1", battleResolution.AttackingPlayerId);
        Assert.Equal("P2", battleResolution.DefendingPlayerId);
        Assert.Equal("P1", battleResolution.WinnerPlayerId);
        Assert.Equal(["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-YI"], battleResolution.AttackerObjectIds);
        Assert.Equal(["P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"], battleResolution.DefenderObjectIds);
        Assert.Equal(["P1-BATTLEFIELD-YI"], battleResolution.SurvivingAttackerObjectIds);
        Assert.Empty(battleResolution.SurvivingDefenderObjectIds);
        Assert.Equal(
            ["P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-BULWARK", "P2-BATTLEFIELD-DEFENDER"],
            battleResolution.DestroyedObjectIds);
        var snapshotBattleResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["battleResolutions"]);
        var snapshotBattleResolution = Assert.Single(snapshotBattleResolutions);
        Assert.Equal("CLOSED", snapshotBattleResolution["kind"]);
        Assert.Equal("P1", snapshotBattleResolution["winnerPlayerId"]);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "OGS·007/024"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-036/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-no-result", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var damageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 4));
        Assert.Contains(damageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-GAREN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 4));

        var noResultEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD:P1-MAIN", noResultEvent.Payload["battlefieldId"]);
        Assert.Equal("P1", noResultEvent.Payload["attackingPlayerId"]);
        Assert.Equal("P2", noResultEvent.Payload["defendingPlayerId"]);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", noResultEvent.Payload["reason"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], Assert.IsType<string[]>(noResultEvent.Payload["attackerObjectIds"]));
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], Assert.IsType<string[]>(noResultEvent.Payload["defenderObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingAttackerObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingDefenderObjectIds"]));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));

        Assert.Equal(1, result.State.Tick);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P1-BATTLEFIELD-GAREN", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-DEFENDER", "GRAVEYARD");
        Assert.Equal(["P1", "P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.False(result.State.BattleState.IsActive);
        var battleResolution = Assert.Single(result.State.BattleResolutions);
        Assert.Equal("NO_RESULT", battleResolution.Kind);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", battleResolution.Reason);
        Assert.Null(battleResolution.WinnerPlayerId);
        Assert.Empty(battleResolution.SurvivingAttackerObjectIds);
        Assert.Empty(battleResolution.SurvivingDefenderObjectIds);
        Assert.Equal(["P1-BATTLEFIELD-GAREN", "P2-BATTLEFIELD-DEFENDER"], battleResolution.DestroyedObjectIds);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandInPriorityWindowIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-priority-window", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithEmptyAttackersIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-empty-attackers", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                [],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithEmptyDefendersIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-empty-defenders", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                [],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithMissingBattlefieldIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-missing-battlefield", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithUnknownBattlefieldIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-battlefield", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MISSING",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithUnsupportedOptionalCostIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unsupported-optional-cost", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithMissingOptionalCostIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-missing-optional-cost", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithAttackerOutsideBattlefieldIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-GAREN"] = new(
                    "P1-BASE-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-attacker-outside-battlefield", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BASE-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BASE-GAREN"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BASE-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BASE-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithDefenderOutsideBattlefieldIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BASE-MUTANT-KITTEN"] = new(
                    "P2-BASE-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-defender-outside-battlefield", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BASE-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Base);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BASE-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BASE-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithOpponentAttackerIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-opponent-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["P1-BATTLEFIELD-GAREN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsAttacking);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithCurrentPlayerDefenderIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-MUTANT-KITTEN"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P1-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P1-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-current-player-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P1-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P1-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithDuplicateAttackerIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-duplicate-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithDuplicateDefenderIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-duplicate-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN", "P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithAttackerAlsoDefenderIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-attacker-defender-overlap", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P1-BATTLEFIELD-GAREN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandByNonActivePlayerIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-non-active-player", "P2", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P2-MAIN",
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["P1-BATTLEFIELD-GAREN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsAttacking);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandOutsideMainPhaseIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            Phase = MatchPhases.TurnEnd,
            TimingState = TimingStates.NeutralClosed,
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-non-main-phase", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchPhases.TurnEnd, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithUnknownAttackerIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-MISSING"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects.ContainsKey("P1-BATTLEFIELD-MISSING"));
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandWithUnknownDefenderIsRejectedUntilCombatSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MISSING"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects.ContainsKey("P2-BATTLEFIELD-MISSING"));
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(1, result.State.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandRejectsAttackerWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-UNKNOWN-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-KNOWN-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-UNKNOWN-ATTACKER"] = new(
                    "P1-BATTLE-UNKNOWN-ATTACKER",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-cardno-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLE-UNKNOWN-ATTACKER"],
                ["P2-BATTLE-KNOWN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-BATTLE-UNKNOWN-ATTACKER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLE-KNOWN-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.CardObjects["P1-BATTLE-UNKNOWN-ATTACKER"].CardNo);
        Assert.False(result.State.CardObjects["P1-BATTLE-UNKNOWN-ATTACKER"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLE-KNOWN-DEFENDER"].IsDefending);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandRejectsDefenderWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-KNOWN-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-UNKNOWN-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "OGS·007/024",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-UNKNOWN-DEFENDER"] = new(
                    "P2-BATTLE-UNKNOWN-DEFENDER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-cardno-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLE-KNOWN-ATTACKER"],
                ["P2-BATTLE-UNKNOWN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-BATTLE-KNOWN-ATTACKER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLE-UNKNOWN-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.CardObjects["P2-BATTLE-UNKNOWN-DEFENDER"].CardNo);
        Assert.False(result.State.CardObjects["P1-BATTLE-KNOWN-ATTACKER"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLE-UNKNOWN-DEFENDER"].IsDefending);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandRejectsBattlefieldDestinationWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                        "P1-BATTLE-KNOWN-ATTACKER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-KNOWN-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-unknown-cardno-battlefield", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                ["P1-BATTLE-KNOWN-ATTACKER"],
                ["P2-BATTLE-KNOWN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.CardObjects["P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION"].CardNo);
        Assert.False(result.State.CardObjects["P1-BATTLE-KNOWN-ATTACKER"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLE-KNOWN-DEFENDER"].IsDefending);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandRejectsOpponentControlledAttackerInPlayerBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-KNOWN-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"] = new(
                    "P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-opponent-controlled-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"],
                ["P2-BATTLE-KNOWN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLE-KNOWN-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P2", result.State.CardObjects["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"].ControllerId);
        Assert.False(result.State.CardObjects["P1-BATTLE-OPPONENT-CONTROLLED-ATTACKER"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLE-KNOWN-DEFENDER"].IsDefending);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandRejectsActingPlayerControlledDefenderInOpponentBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-KNOWN-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"] = new(
                    "P2-BATTLE-ACTING-CONTROLLED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-acting-controlled-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLE-KNOWN-ATTACKER"],
                ["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-BATTLE-KNOWN-ATTACKER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P1", result.State.CardObjects["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"].ControllerId);
        Assert.False(result.State.CardObjects["P1-BATTLE-KNOWN-ATTACKER"].IsAttacking);
        Assert.False(result.State.CardObjects["P2-BATTLE-ACTING-CONTROLLED-DEFENDER"].IsDefending);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandSingleCombatantsFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-single-combatants.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].Damage);
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandBulwarkBackRowAssignmentFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-bulwark-back-row-assignment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-VOLIBEAR"], result.FinalState.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-LEBLANC", "P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLEFIELD-VOLIBEAR"].Damage);
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-BATTLEFIELD-LEBLANC", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandHuntConquestExperienceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-hunt-conquest-experience.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Equal(3, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(0, result.FinalState.PlayerExperience["P2"]);
        Assert.Equal(["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"], result.FinalState.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"].IsAttacking);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLEFIELD-GLUTTONOUS-TOADFROG"].Damage);
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandEmptyAttackersRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-empty-attackers-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandEmptyDefendersRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-empty-defenders-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandMissingBattlefieldRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-missing-battlefield-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandUnknownBattlefieldRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-unknown-battlefield-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandUnsupportedOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-unsupported-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandMissingOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-missing-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAttackerOutsideBattlefieldRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-attacker-outside-battlefield-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BASE-GAREN"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BASE-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandDefenderOutsideBattlefieldRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-defender-outside-battlefield-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Base);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BASE-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandOpponentAttackerRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-opponent-attacker-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandCurrentPlayerDefenderRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-current-player-defender-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN", "P1-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandDuplicateAttackerRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-duplicate-attacker-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandDuplicateDefenderRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-duplicate-defender-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandAttackerDefenderOverlapRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-attacker-defender-overlap-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandNonActivePlayerRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-non-active-player-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandNonMainPhaseRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-non-main-phase-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(MatchPhases.TurnEnd, result.FinalState.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.FinalState.TimingState);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandUnknownAttackerRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-unknown-attacker-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-BATTLEFIELD-MISSING"));
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandUnknownDefenderRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-unknown-defender-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P2-BATTLEFIELD-MISSING"));
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandPriorityWindowRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-declare-battle-priority-window-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal("NEUTRAL_CLOSED", result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-MUTANT-KITTEN"].IsDefending);
        var stackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSpellshieldTaxWhenPowerIsInsufficient()
    {
        var state = P4SpellshieldTaxState(mana: 2);
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-spellshield-tax-insufficient", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-INCINERATE",
                "OGS·003/024",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4IncinerateSpellshieldTaxInsufficientFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-incinerate-spellshield-tax-insufficient-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMultipleSpellshieldTaxWhenPowerIsInsufficient()
    {
        var state = P4MultipleSpellshieldTaxState(mana: 5);
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-multiple-spellshield-tax-insufficient", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SPIRIT-FIRE",
                "OGN·256/298",
                ["P2-SPIRIT-FIRE-SPELLSHIELD-001", "P2-SPIRIT-FIRE-SPELLSHIELD2-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["BF", "P2-SPIRIT-FIRE-SPELLSHIELD-001", "P2-SPIRIT-FIRE-SPELLSHIELD2-001"],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MultipleSpellshieldTaxInsufficientFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["BF", "P2-SPIRIT-FIRE-SPELLSHIELD-001", "P2-SPIRIT-FIRE-SPELLSHIELD2-001", "P2-SPIRIT-FIRE-KEEPER-001"],
            result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Theory]
    [InlineData("p2-preflight-play-dorans-shield-equipment.fixture.json")]
    [InlineData("p2-preflight-play-long-sword-agile-equipment.fixture.json")]
    [InlineData("p2-preflight-play-sentinel-adept-no-optional-assemble.fixture.json")]
    [InlineData("p2-preflight-play-stout-poro-no-optional-assemble.fixture.json")]
    [InlineData("p2-preflight-play-sfd-ornn-no-optional-assemble-spellshield2.fixture.json")]
    [InlineData("p4-assemble-equipment-long-sword-attach.fixture.json")]
    [InlineData("p4-assemble-equipment-priority-window-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-already-attached-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-missing-target-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-unknown-target-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-missing-source-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-unknown-source-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-hand-source-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-opponent-source-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-non-equipment-source-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-non-unit-target-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-unsupported-optional-cost-rejected.fixture.json")]
    [InlineData("p4-assemble-equipment-missing-optional-cost-rejected.fixture.json")]
    [InlineData("p4-move-unit-attached-equipment-source-rejected.fixture.json")]
    public async Task P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Theory]
    [InlineData("p2-preflight-play-take-up-attach-weapon-draw.fixture.json", "P2-UNIT-TAKE-UP-001")]
    [InlineData("p2-preflight-play-take-up-detach-weapon-draw.fixture.json", null)]
    public async Task P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen(
        string fixtureFileName,
        string? expectedAttachedToObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            expectedAttachedToObjectId,
            result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].AttachedToObjectId);
        Assert.Single(result.EventKinds, eventKind => string.Equals(
            eventKind,
            expectedAttachedToObjectId is null ? "EQUIPMENT_DETACHED" : "EQUIPMENT_ATTACHED",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4EquipmentAttachmentRejectsOpponentControlledWeaponInControllersZone()
    {
        var state = new MatchState(
            "p4-take-up-dirty-control-room",
            0,
            4,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-TAKE-UP"],
                    Base = ["P2-EQUIPMENT-TAKE-UP-WEAPON"],
                    Battlefields = ["P2-UNIT-TAKE-UP-001"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-TAKE-UP-001"] = new(
                    "P2-UNIT-TAKE-UP-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-EQUIPMENT-TAKE-UP-WEAPON"] = new(
                    "P2-EQUIPMENT-TAKE-UP-WEAPON",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P2",
                    controllerId: "P1")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-TAKE-UP-001"],
                    damageAmount: 2)
            ]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-take-up-dirty-control", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-TAKE-UP",
                "SFD·011/221",
                ["P2-UNIT-TAKE-UP-001", "P2-EQUIPMENT-TAKE-UP-WEAPON"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Contains("P2-SPELL-TAKE-UP", result.State.PlayerZones["P2"].Hand);
        Assert.Null(result.State.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].AttachedToObjectId);
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], result.State.StackItems.Select(stackItem => stackItem.StackItemId).ToArray());
    }

    [Theory]
    [InlineData("p4-ephemeral-destroys-controlled-objects-turn-start.fixture.json")]
    [InlineData("p2-preflight-play-scrying-shell-equipment-predict-recycle.fixture.json")]
    [InlineData("p4-scrying-shell-predict-outside-top-card-rejected.fixture.json")]
    [InlineData("p2-preflight-play-ogn-kogmaw-last-breath-static.fixture.json")]
    public async Task P4LifecycleKeywordProfilesKeepExistingRepresentativeFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Theory]
    [InlineData("p4-hide-card-standby-face-down.fixture.json")]
    [InlineData("p4-hide-card-standby-insufficient-cost-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-source-outside-hand-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-opponent-hand-source-rejected.fixture.json")]
    [InlineData("p4-hide-card-known-non-standby-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-unsupported-destination-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-unsupported-optional-cost-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-missing-cost-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-window-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-pending-stack-rejected.fixture.json")]
    [InlineData("p4-hide-card-standby-cardno-mismatch-rejected.fixture.json")]
    [InlineData("p4-guerrilla-warfare-free-standby-hide.fixture.json")]
    [InlineData("p4-hide-card-standby-free-without-permission-rejected.fixture.json")]
    [InlineData("p4-guerrilla-warfare-non-standby-target-rejected.fixture.json")]
    [InlineData("p4-play-center-stage-echo-insufficient-mana-rejected.fixture.json")]
    [InlineData("p4-play-punishment-illegal-echo-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-base.fixture.json")]
    [InlineData("p4-reveal-card-standby-cardno-mismatch-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-source-outside-base-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-source-not-face-down-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-target-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-unsupported-optional-cost-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-missing-cost-rejected.fixture.json")]
    [InlineData("p4-reveal-card-known-non-standby-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-known-non-standby-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-missing-cost-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-unsupported-optional-cost-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-cardno-mismatch-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-unsupported-destination-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-source-outside-base-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-source-not-face-down-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-unsupported-destination-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-target-rejected.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-stack.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-teemo-self-power.fixture.json")]
    [InlineData("p4-reveal-card-standby-reaction-without-priority-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-premodel-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-target-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-base-destination-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-priority-window-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-gloomy-apothecary-battlefield.fixture.json")]
    [InlineData("p4-ambush-play-card-source-outside-hand-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-unknown-source-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-opponent-hand-source-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-non-ambush-source-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-cardno-mismatch-rejected.fixture.json")]
    [InlineData("p4-ambush-play-card-optional-cost-rejected.fixture.json")]
    [InlineData("p4-play-existential-dread-friendly-attacking-target-rejected.fixture.json")]
    [InlineData("p2-preflight-play-pakaa-cub-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-gloomy-apothecary-return-friendly-battlefield.fixture.json")]
    [InlineData("p2-preflight-play-vi-alt-a-ambush-attack-stun-static.fixture.json")]
    public async Task P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches)
            + "\nactual event kinds: " + string.Join(", ", result.EventKinds)
            + "\nactual stack: " + JsonSerializer.Serialize(result.FinalState.StackItems)
            + "\nactual source: " + JsonSerializer.Serialize(result.FinalState.CardObjects.GetValueOrDefault("SHADOW")));
    }

    [Theory]
    [InlineData("p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json")]
    [InlineData("p2-preflight-play-portalpal-rescue-banish-play-base.fixture.json")]
    [InlineData("p2-preflight-play-secret-art-mercy-grant-boon.fixture.json")]
    [InlineData("p2-preflight-play-shepherds-heirloom-weapon-equipment.fixture.json")]
    [InlineData("p4-move-unit-command-premodel-rejected.fixture.json")]
    [InlineData("p4-move-unit-roam-precise-battlefield.fixture.json")]
    [InlineData("p4-move-unit-precise-destination-rejected.fixture.json")]
    [InlineData("p4-move-unit-precise-origin-rejected.fixture.json")]
    [InlineData("p4-move-unit-base-to-battlefield.fixture.json")]
    [InlineData("p4-move-unit-battlefield-to-base.fixture.json")]
    [InlineData("p4-move-unit-opponent-source-rejected.fixture.json")]
    [InlineData("p4-move-unit-face-down-source-rejected.fixture.json")]
    [InlineData("p4-move-unit-non-unit-source-rejected.fixture.json")]
    [InlineData("p4-move-unit-same-zone-destination-rejected.fixture.json")]
    [InlineData("p4-move-unit-window-rejected.fixture.json")]
    [InlineData("p4-move-unit-pending-stack-rejected.fixture.json")]
    [InlineData("p4-move-unit-optional-cost-rejected.fixture.json")]
    [InlineData("p4-move-unit-unsupported-zone-rejected.fixture.json")]
    [InlineData("p4-move-unit-origin-mismatch-rejected.fixture.json")]
    [InlineData("p4-move-unit-hand-source-rejected.fixture.json")]
    [InlineData("p4-move-unit-combatant-source-rejected.fixture.json")]
    [InlineData("p4-move-unit-attached-equipment-source-rejected.fixture.json")]
    [InlineData("p4-play-poppy-spend-experience-reduce-cost.fixture.json")]
    [InlineData("p4-play-wuji-apprentice-level6-draw.fixture.json")]
    [InlineData("p4-play-stern-sergeant-dynamic-experience.fixture.json")]
    [InlineData("p4-play-spirit-fire-total-power-too-high-rejected.fixture.json")]
    [InlineData("p4-play-playful-tentacles-total-power-too-high-rejected.fixture.json")]
    [InlineData("p4-play-hunt-the-weak-target-power-too-high-rejected.fixture.json")]
    [InlineData("p4-play-gust-target-power-too-high-rejected.fixture.json")]
    [InlineData("p4-play-highway-robbery-off-field-target-rejected.fixture.json")]
    [InlineData("p4-play-deadly-flourish-friendly-target-rejected.fixture.json")]
    [InlineData("p4-play-blood-money-target-power-too-high-rejected.fixture.json")]
    [InlineData("p4-play-punishment-base-unit-target-rejected.fixture.json")]
    [InlineData("p4-play-kerplunk-non-attacking-target-rejected.fixture.json")]
    [InlineData("p4-play-zenith-blade-base-unit-target-rejected.fixture.json")]
    [InlineData("p4-play-zenith-blade-friendly-target-rejected.fixture.json")]
    [InlineData("p4-play-last-breath-enemy-base-target-rejected.fixture.json")]
    [InlineData("p4-play-last-breath-target-order-rejected.fixture.json")]
    [InlineData("p4-play-convergent-mutation-enemy-target-rejected.fixture.json")]
    [InlineData("p4-play-convergent-mutation-duplicate-target-rejected.fixture.json")]
    [InlineData("p4-play-thundering-sky-insufficient-reduced-cost-rejected.fixture.json")]
    [InlineData("p4-play-mind-and-balance-insufficient-unreduced-cost-rejected.fixture.json")]
    [InlineData("p4-play-piercing-light-repeated-target-rejected.fixture.json")]
    [InlineData("p4-play-bellows-breath-fourth-target-rejected.fixture.json")]
    [InlineData("p4-play-bellows-breath-repeated-target-rejected.fixture.json")]
    [InlineData("p4-play-rocket-barrage-missing-mode-rejected.fixture.json")]
    [InlineData("p4-play-rocket-barrage-destroy-equipment-unit-target-rejected.fixture.json")]
    [InlineData("p4-play-emergency-recall-unit-target-rejected.fixture.json")]
    [InlineData("p4-play-poro-snax-target-rejected.fixture.json")]
    [InlineData("p4-play-shurelyas-requiem-target-rejected.fixture.json")]
    [InlineData("p4-play-future-forge-target-rejected.fixture.json")]
    [InlineData("p4-play-scrap-heap-target-rejected.fixture.json")]
    [InlineData("p4-play-sprite-lantern-target-rejected.fixture.json")]
    [InlineData("p4-play-sumpworks-map-target-rejected.fixture.json")]
    [InlineData("p4-play-scrying-blossom-target-rejected.fixture.json")]
    [InlineData("p4-play-magic-beans-target-rejected.fixture.json")]
    [InlineData("p4-play-steel-ballista-target-rejected.fixture.json")]
    [InlineData("p4-play-heart-of-ice-target-rejected.fixture.json")]
    [InlineData("p4-play-remorse-orb-target-rejected.fixture.json")]
    [InlineData("p4-play-soul-sword-target-rejected.fixture.json")]
    [InlineData("p4-play-jagged-dirk-target-rejected.fixture.json")]
    [InlineData("p4-play-dorans-shield-target-rejected.fixture.json")]
    [InlineData("p4-play-hextech-infused-bulwark-target-rejected.fixture.json")]
    [InlineData("p4-play-dorans-blade-target-rejected.fixture.json")]
    [InlineData("p4-play-dorans-ring-target-rejected.fixture.json")]
    [InlineData("p4-play-vanguards-eye-target-rejected.fixture.json")]
    [InlineData("p4-play-recurve-bow-target-rejected.fixture.json")]
    [InlineData("p4-play-long-sword-target-rejected.fixture.json")]
    [InlineData("p4-play-steraks-gage-target-rejected.fixture.json")]
    [InlineData("p4-play-cloth-armor-target-rejected.fixture.json")]
    [InlineData("p4-play-spinning-axe-target-rejected.fixture.json")]
    [InlineData("p4-play-shepherds-heirloom-target-rejected.fixture.json")]
    [InlineData("p4-play-brutalizer-target-rejected.fixture.json")]
    [InlineData("p4-play-guardian-angel-target-rejected.fixture.json")]
    [InlineData("p4-play-hexdrinker-target-rejected.fixture.json")]
    [InlineData("p4-play-warmogs-armor-target-rejected.fixture.json")]
    [InlineData("p4-play-trinity-force-target-rejected.fixture.json")]
    [InlineData("p4-play-boots-of-swiftness-target-rejected.fixture.json")]
    [InlineData("p4-play-cull-target-rejected.fixture.json")]
    [InlineData("p4-play-sacred-shears-target-rejected.fixture.json")]
    [InlineData("p4-play-bf-sword-target-rejected.fixture.json")]
    [InlineData("p4-play-wanderers-guidebook-target-rejected.fixture.json")]
    [InlineData("p4-play-arions-fall-target-rejected.fixture.json")]
    [InlineData("p4-play-hunters-machete-target-rejected.fixture.json")]
    [InlineData("p4-play-withered-battleaxe-target-rejected.fixture.json")]
    [InlineData("p4-play-bone-club-target-rejected.fixture.json")]
    [InlineData("p4-play-ancient-stele-target-rejected.fixture.json")]
    [InlineData("p4-play-hextech-anomaly-target-rejected.fixture.json")]
    [InlineData("p4-play-energy-channel-target-rejected.fixture.json")]
    [InlineData("p4-play-time-gate-target-rejected.fixture.json")]
    [InlineData("p4-play-raven-tome-target-rejected.fixture.json")]
    [InlineData("p4-play-sun-disc-target-rejected.fixture.json")]
    [InlineData("p4-play-foresight-mask-target-rejected.fixture.json")]
    [InlineData("p4-play-solari-altar-target-rejected.fixture.json")]
    [InlineData("p4-play-chemtech-barrel-target-rejected.fixture.json")]
    [InlineData("p4-play-soul-wheel-target-rejected.fixture.json")]
    [InlineData("p4-play-mushroom-bag-target-rejected.fixture.json")]
    [InlineData("p4-play-arena-bar-target-rejected.fixture.json")]
    [InlineData("p4-play-pirate-hideout-target-rejected.fixture.json")]
    [InlineData("p4-play-forgotten-signpost-target-rejected.fixture.json")]
    [InlineData("p4-play-frozen-gem-target-rejected.fixture.json")]
    [InlineData("p4-play-crumbling-palace-target-rejected.fixture.json")]
    [InlineData("p4-play-scarlet-rose-target-rejected.fixture.json")]
    [InlineData("p4-play-reversal-shard-target-rejected.fixture.json")]
    [InlineData("p4-play-assembly-rack-target-rejected.fixture.json")]
    [InlineData("p4-play-sfur-song-target-rejected.fixture.json")]
    [InlineData("p4-play-z-drive-target-rejected.fixture.json")]
    [InlineData("p4-play-vanguard-armory-target-rejected.fixture.json")]
    [InlineData("p4-play-remembrance-altar-target-rejected.fixture.json")]
    [InlineData("p4-play-rage-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-focus-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-insight-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-power-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-discord-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-unity-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-rage-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-focus-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-insight-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-power-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-discord-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-unity-sigil-target-rejected.fixture.json")]
    [InlineData("p4-play-wondrous-pack-target-rejected.fixture.json")]
    [InlineData("p4-play-siren-target-rejected.fixture.json")]
    [InlineData("p4-play-ownerless-treasure-target-rejected.fixture.json")]
    [InlineData("p4-play-scavenging-whiz-target-rejected.fixture.json")]
    [InlineData("p4-play-mistfall-bladeyard-target-rejected.fixture.json")]
    [InlineData("p4-play-shimmering-aurora-target-rejected.fixture.json")]
    [InlineData("p4-play-solari-emblem-target-rejected.fixture.json")]
    [InlineData("p4-play-vanguard-helm-target-rejected.fixture.json")]
    [InlineData("p4-play-honeyfruit-target-rejected.fixture.json")]
    [InlineData("p4-play-last-rites-target-rejected.fixture.json")]
    [InlineData("p4-play-blade-of-ruined-king-target-rejected.fixture.json")]
    [InlineData("p4-play-mysterious-weapon-target-rejected.fixture.json")]
    [InlineData("p4-play-sea-monster-hook-target-rejected.fixture.json")]
    [InlineData("p4-play-petricite-monument-target-rejected.fixture.json")]
    [InlineData("p4-play-zhonyas-hourglass-target-rejected.fixture.json")]
    [InlineData("p4-play-edge-of-night-target-rejected.fixture.json")]
    [InlineData("p4-play-hearthfire-cloak-target-rejected.fixture.json")]
    [InlineData("p4-play-rabadons-deathcap-target-rejected.fixture.json")]
    [InlineData("p4-play-blast-cone-target-rejected.fixture.json")]
    [InlineData("p4-play-death-list-target-rejected.fixture.json")]
    [InlineData("p4-play-cursed-sarcophagus-target-rejected.fixture.json")]
    [InlineData("p4-play-boneclub-promo-target-rejected.fixture.json")]
    [InlineData("p4-play-hextech-gauntlet-target-rejected.fixture.json")]
    [InlineData("p4-play-treasure-golem-target-rejected.fixture.json")]
    [InlineData("p4-play-xerath-target-rejected.fixture.json")]
    [InlineData("p4-play-dragon-soul-sage-target-rejected.fixture.json")]
    [InlineData("p4-play-fluft-poro-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-088-renata-glasc-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-088a-renata-glasc-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-028-draven-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-110-fiora-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-110a-fiora-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-141-irelia-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-141a-irelia-target-rejected.fixture.json")]
    [InlineData("p4-play-dragon-caller-target-rejected.fixture.json")]
    [InlineData("p4-play-waterbender-target-rejected.fixture.json")]
    [InlineData("p4-play-wise-elder-target-rejected.fixture.json")]
    [InlineData("p4-play-eager-apprentice-target-rejected.fixture.json")]
    [InlineData("p4-play-arena-service-crew-target-rejected.fixture.json")]
    [InlineData("p4-play-poro-herder-target-rejected.fixture.json")]
    [InlineData("p4-play-ravenbloom-student-target-rejected.fixture.json")]
    [InlineData("p4-play-resonant-soul-target-rejected.fixture.json")]
    [InlineData("p4-play-bilgewater-bully-target-rejected.fixture.json")]
    [InlineData("p4-play-sharpshooter-pirate-target-rejected.fixture.json")]
    [InlineData("p4-play-dune-drake-target-rejected.fixture.json")]
    [InlineData("p4-play-ember-monk-target-rejected.fixture.json")]
    [InlineData("p4-play-hidden-tracker-target-rejected.fixture.json")]
    [InlineData("p4-play-undercover-agent-target-rejected.fixture.json")]
    [InlineData("p4-play-traveling-merchant-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-kogmaw-target-rejected.fixture.json")]
    [InlineData("p4-play-noxian-drummer-target-rejected.fixture.json")]
    [InlineData("p4-play-tide-caller-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-202-jinx-target-rejected.fixture.json")]
    [InlineData("p4-play-ghost-matron-target-rejected.fixture.json")]
    [InlineData("p4-play-albus-ferros-target-rejected.fixture.json")]
    [InlineData("p4-play-ogn-karthus-target-rejected.fixture.json")]
    [InlineData("p4-play-dunehorn-beast-target-rejected.fixture.json")]
    [InlineData("p4-play-gloompath-guard-target-rejected.fixture.json")]
    [InlineData("p4-play-apprentice-blacksmith-target-rejected.fixture.json")]
    [InlineData("p4-play-mountain-ape-elder-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-089-rumble-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-089a-rumble-target-rejected.fixture.json")]
    [InlineData("p4-play-yordle-explorer-target-rejected.fixture.json")]
    [InlineData("p4-play-black-market-broker-target-rejected.fixture.json")]
    [InlineData("p4-play-hunting-sea-crew-target-rejected.fixture.json")]
    [InlineData("p4-play-ghostly-centaur-target-rejected.fixture.json")]
    [InlineData("p4-play-corrupt-enforcer-target-rejected.fixture.json")]
    [InlineData("p4-play-mighty-faerie-target-rejected.fixture.json")]
    [InlineData("p4-play-jar-meddarda-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-vex-target-rejected.fixture.json")]
    [InlineData("p4-play-prominent-patron-target-rejected.fixture.json")]
    [InlineData("p4-play-royal-attendant-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-049-aphelios-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-058-ornn-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-058a-ornn-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-079-bard-target-rejected.fixture.json")]
    [InlineData("p4-play-big-swindler-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-jayce-target-rejected.fixture.json")]
    [InlineData("p4-play-zaunite-thug-target-rejected.fixture.json")]
    [InlineData("p4-play-glasc-bartender-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-170-reksai-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-170a-reksai-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-soraka-target-rejected.fixture.json")]
    [InlineData("p4-play-gai-beast-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-180-fiora-target-rejected.fixture.json")]
    [InlineData("p4-play-sfd-180a-fiora-target-rejected.fixture.json")]
    [InlineData("p4-play-marching-orders-enemy-base-target-rejected.fixture.json")]
    [InlineData("p4-play-duel-target-order-rejected.fixture.json")]
    [InlineData("p4-play-battle-command-target-order-rejected.fixture.json")]
    [InlineData("p4-play-void-assault-target-order-rejected.fixture.json")]
    [InlineData("p4-play-firestorm-explicit-unit-target-rejected.fixture.json")]
    [InlineData("p4-play-crescent-strike-friendly-target-rejected.fixture.json")]
    [InlineData("p4-play-crescent-strike-base-target-rejected.fixture.json")]
    [InlineData("p4-play-switcheroo-duplicate-target-rejected.fixture.json")]
    [InlineData("p4-play-switcheroo-base-target-rejected.fixture.json")]
    public async Task P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches)
            + "\nactual event kinds: " + string.Join(", ", result.EventKinds)
            + "\nactual stack: " + JsonSerializer.Serialize(result.FinalState.StackItems)
            + "\nactual source: " + JsonSerializer.Serialize(result.FinalState.CardObjects.GetValueOrDefault("SHADOW")));
    }
}
