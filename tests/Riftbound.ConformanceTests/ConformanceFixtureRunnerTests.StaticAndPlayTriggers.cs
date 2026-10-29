using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P79FriendlyFilteredStaticKeywordGrantsMultipleNonCombatKeywordsToMatchingFriendlyUnits()
    {
        var state = FriendlyFilteredMultiKeywordStaticAuraState() with
        {
            RunePools = new Dictionary<string, RunePool>
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            }
        };

        var p1RoamerKeywords = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-SPEEDING-MECH-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-SPEEDING-MECH-ROAMER", StringComparison.Ordinal))
            .Select(effect => effect.EffectId.Split(':').Last())
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([CardResourceKeywordNames.Spellshield, CardCombatKeywordNames.Roam], p1RoamerKeywords);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-SPEEDING-MECH-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-SPEEDING-NONMECH", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-SPEEDING-MECH-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-SPEEDING-MECH-TARGET", StringComparison.Ordinal));

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        Assert.Contains(sourceRequirements, requirement =>
            string.Equals(requirement["sourceObjectId"] as string, "P1-SPEEDING-MECH-ROAMER", StringComparison.Ordinal)
            && string.Equals(requirement["mode"] as string, "ROAM", StringComparison.Ordinal));

        var moveResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-friendly-filtered-multi-keyword-roam", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-SPEEDING-MECH-ROAMER",
                "BATTLEFIELD:P1-SPEEDING-ORIGIN",
                "BATTLEFIELD:P1-SPEEDING-DESTINATION",
                ["ROAM"]),
            CancellationToken.None);

        Assert.True(moveResult.Accepted, moveResult.ErrorMessage);
        Assert.Equal(new[] {"UNIT_MOVED_TO_BATTLEFIELD", "BATTLEFIELD_CONTROL_LOST"}, moveResult.Events.Select(e => e.Kind));
        var moveEvent = Assert.Single(moveResult.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", moveEvent.Kind);
        Assert.Equal(CardCombatKeywordNames.Roam, moveEvent.Payload["movementKeyword"]);
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, moveResult.State.CardObjects["P1-SPEEDING-MECH-ROAMER"].Tags);

        var spellResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-friendly-filtered-multi-keyword-spellshield", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-INCINERATE",
                "OGS·003/024",
                ["P2-SPEEDING-MECH-TARGET"]),
            CancellationToken.None);

        Assert.True(spellResult.Accepted, spellResult.ErrorMessage);
        var costPaidEvent = Assert.Single(spellResult.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(2, costPaidEvent.Payload["mana"]);
        Assert.Equal(2, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(2, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(1, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Equal(
            ["P2-SPEEDING-MECH-TARGET"],
            Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task P79FriendlyFilteredStaticKeywordBulwarkSupportsMultiDefenderAssignment()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-LILLIA-BATTLEFIELD", "P1-LILLIA-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-LILLIA-SOURCE"],
                    Battlefields = ["P2-LILLIA-TOKEN", "P2-LILLIA-OTHER-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LILLIA-BATTLEFIELD"] = new(
                    "P1-LILLIA-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LILLIA-ATTACKER"] = new(
                    "P1-LILLIA-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-LILLIA-SOURCE"] = new(
                    "P2-LILLIA-SOURCE",
                    cardNo: "UNL-058/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LILLIA-TOKEN"] = new(
                    "P2-LILLIA-TOKEN",
                    cardNo: "UNL·T07",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LILLIA-OTHER-DEFENDER"] = new(
                    "P2-LILLIA-OTHER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-LILLIA-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-LILLIA-BATTLEFIELD"),
                ["P1-LILLIA-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-LILLIA-BATTLEFIELD"),
                ["P2-LILLIA-SOURCE"] = new("P2", "BASE"),
                ["P2-LILLIA-TOKEN"] = new("P2", "BATTLEFIELD", "P1-LILLIA-BATTLEFIELD"),
                ["P2-LILLIA-OTHER-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-LILLIA-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-LILLIA-BATTLEFIELD")]
        };

        var ruleTextAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P2-LILLIA-SOURCE", StringComparison.Ordinal));
        Assert.Equal("P2-LILLIA-TOKEN", ruleTextAura.TargetObjectId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-friendly-filtered-static-keyword-bulwark", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-LILLIA-BATTLEFIELD",
                ["P1-LILLIA-ATTACKER"],
                ["P2-LILLIA-TOKEN", "P2-LILLIA-OTHER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79SoulShepherdAddsPowerToControlledTokenUnits()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-SOUL-SHEPHERD",
                        "P1-SOUL-SHEPHERD-FACEDOWN",
                        "P1-SOUL-SHEPHERD-STANDBY"
                    ],
                    Battlefields =
                    [
                        "P1-CATALOG-WARHAWK",
                        "P1-NORMAL-ATTACKER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SOUL-SHEPHERD"],
                    Battlefields = ["P2-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SOUL-SHEPHERD"] = new(
                    "P1-SOUL-SHEPHERD",
                    cardNo: "UNL-077/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SOUL-SHEPHERD-FACEDOWN"] = new(
                    "P1-SOUL-SHEPHERD-FACEDOWN",
                    cardNo: "UNL-077/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    isFaceDown: true,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SOUL-SHEPHERD-STANDBY"] = new(
                    "P1-SOUL-SHEPHERD-STANDBY",
                    isFaceDown: true,
                    cardNo: "UNL-077/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CATALOG-WARHAWK"] = new(
                    "P1-CATALOG-WARHAWK",
                    cardNo: "UNL·T02",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-NORMAL-ATTACKER"] = new(
                    "P1-NORMAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SOUL-SHEPHERD"] = new(
                    "P2-SOUL-SHEPHERD",
                    cardNo: "UNL-077/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 30,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-soul-shepherd-token-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-CATALOG-WARHAWK", "P1-NORMAL-ATTACKER"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, attackerDamageEvents.Length);

        var tokenDamageEvent = Assert.Single(attackerDamageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-CATALOG-WARHAWK", StringComparison.Ordinal));
        Assert.Equal(1, tokenDamageEvent.Payload["basePower"]);
        Assert.Equal(1, tokenDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(2, tokenDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, tokenDamageEvent.Payload["damage"]);

        var nonTokenDamageEvent = Assert.Single(attackerDamageEvents, gameEvent =>
            string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-NORMAL-ATTACKER", StringComparison.Ordinal));
        Assert.Equal(2, nonTokenDamageEvent.Payload["basePower"]);
        Assert.False(nonTokenDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, nonTokenDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, nonTokenDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base =
                    [
                        "P1-SAVAGE-JAWFISH",
                        "P1-SAVAGE-JAWFISH-FACEDOWN",
                        "P1-SAVAGE-JAWFISH-STANDBY",
                        "P1-FRIENDLY-TARGET"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SAVAGE-JAWFISH"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SAVAGE-JAWFISH"] = new(
                    "P1-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SAVAGE-JAWFISH-FACEDOWN"] = new(
                    "P1-SAVAGE-JAWFISH-FACEDOWN",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    isFaceDown: true,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SAVAGE-JAWFISH-STANDBY"] = new(
                    "P1-SAVAGE-JAWFISH-STANDBY",
                    isFaceDown: true,
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-TARGET"] = new(
                    "P1-FRIENDLY-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SAVAGE-JAWFISH"] = new(
                    "P2-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-savage-jawfish-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-FRIENDLY-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-savage-jawfish-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-savage-jawfish-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var destroyedEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal("P1-FRIENDLY-TARGET", destroyedEvent.Payload["targetObjectId"]);
        Assert.Equal("P1", destroyedEvent.Payload["ownerPlayerId"]);

        var queuedEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals((gameEvent.Payload.GetValueOrDefault("observerKind") ?? gameEvent.Payload["effectKind"]) as string, "SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", StringComparison.Ordinal));
        Assert.Equal("P1-SAVAGE-JAWFISH", queuedEvent.Payload["sourceObjectId"]);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.TriggerQueue);
        var savageTriggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1", savageTriggerStackItem.ControllerId);
        Assert.Equal("P1-SAVAGE-JAWFISH", savageTriggerStackItem.SourceObjectId);
        Assert.Equal("SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", savageTriggerStackItem.DeathObserver!.Kind);
        Assert.Equal("P1", p2Pass.State.PriorityPlayerId);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(0, p2Pass.State.PlayerExperience["P1"]);
        Assert.Equal(0, p2Pass.State.PlayerExperience["P2"]);

        var triggerP1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-savage-jawfish-trigger-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var triggerP2Pass = await engine.ResolveAsync(
            triggerP1Pass.State,
            new PlayerIntent("intent-p7-9-savage-jawfish-trigger-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(triggerP1Pass.Accepted, triggerP1Pass.ErrorMessage);
        Assert.True(triggerP2Pass.Accepted, triggerP2Pass.ErrorMessage);
        var experienceEvent = Assert.Single(triggerP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P1", experienceEvent.Payload["playerId"]);
        Assert.Equal("P1-SAVAGE-JAWFISH", experienceEvent.Payload["sourceObjectId"]);
        Assert.Equal("UNL-129/219", experienceEvent.Payload["cardNo"]);
        Assert.Equal(1, experienceEvent.Payload["amount"]);
        Assert.Equal(1, experienceEvent.Payload["totalExperience"]);
        Assert.Empty(triggerP2Pass.State.StackItems);
        Assert.Equal(1, triggerP2Pass.State.PlayerExperience["P1"]);
        Assert.Equal(0, triggerP2Pass.State.PlayerExperience["P2"]);
        Assert.Contains("P1-SAVAGE-JAWFISH", triggerP2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-FRIENDLY-TARGET", triggerP2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-SPELL-VENGEANCE", triggerP2Pass.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(triggerP2Pass.State, "P1-FRIENDLY-TARGET", "GRAVEYARD");
    }

    [Fact]
    public async Task P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base =
                    [
                        "P1-GHOSTLY-CENTAUR",
                        "P1-GHOSTLY-CENTAUR-FACEDOWN",
                        "P1-GHOSTLY-CENTAUR-STANDBY",
                        "P1-FRIENDLY-TARGET"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-GHOSTLY-CENTAUR"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GHOSTLY-CENTAUR"] = new(
                    "P1-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GHOSTLY-CENTAUR-FACEDOWN"] = new(
                    "P1-GHOSTLY-CENTAUR-FACEDOWN",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    isFaceDown: true,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GHOSTLY-CENTAUR-STANDBY"] = new(
                    "P1-GHOSTLY-CENTAUR-STANDBY",
                    isFaceDown: true,
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-TARGET"] = new(
                    "P1-FRIENDLY-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-GHOSTLY-CENTAUR"] = new(
                    "P2-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ghostly-centaur-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-FRIENDLY-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-ghostly-centaur-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-ghostly-centaur-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var destroyedEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal("P1-FRIENDLY-TARGET", destroyedEvent.Payload["targetObjectId"]);
        Assert.Equal("P1", destroyedEvent.Payload["ownerPlayerId"]);

        var queuedEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals((gameEvent.Payload.GetValueOrDefault("observerKind") ?? gameEvent.Payload["effectKind"]) as string, "GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", StringComparison.Ordinal));
        Assert.Equal("P1-GHOSTLY-CENTAUR", queuedEvent.Payload["sourceObjectId"]);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.TriggerQueue);
        var ghostlyTriggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1", ghostlyTriggerStackItem.ControllerId);
        Assert.Equal("P1-GHOSTLY-CENTAUR", ghostlyTriggerStackItem.SourceObjectId);
        Assert.Equal("GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", ghostlyTriggerStackItem.DeathObserver!.Kind);
        Assert.Equal("P1", p2Pass.State.PriorityPlayerId);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal(5, p2Pass.State.CardObjects["P1-GHOSTLY-CENTAUR"].Power);

        var triggerP1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-ghostly-centaur-trigger-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var triggerP2Pass = await engine.ResolveAsync(
            triggerP1Pass.State,
            new PlayerIntent("intent-p7-9-ghostly-centaur-trigger-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(triggerP1Pass.Accepted, triggerP1Pass.ErrorMessage);
        Assert.True(triggerP2Pass.Accepted, triggerP2Pass.ErrorMessage);
        var powerEvent = Assert.Single(triggerP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal("P1-GHOSTLY-CENTAUR", powerEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-GHOSTLY-CENTAUR", powerEvent.Payload["targetObjectId"]);
        Assert.Equal(2, powerEvent.Payload["powerDelta"]);
        Assert.Equal(2, powerEvent.Payload["appliedPowerDelta"]);
        Assert.Equal(7, powerEvent.Payload["resultingPower"]);
        Assert.Empty(triggerP2Pass.State.StackItems);
        Assert.Equal(7, triggerP2Pass.State.CardObjects["P1-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(2, triggerP2Pass.State.CardObjects["P1-GHOSTLY-CENTAUR"].UntilEndOfTurnPowerModifier);
        Assert.Equal(5, triggerP2Pass.State.CardObjects["P1-GHOSTLY-CENTAUR-FACEDOWN"].Power);
        Assert.Equal(5, triggerP2Pass.State.CardObjects["P1-GHOSTLY-CENTAUR-STANDBY"].Power);
        Assert.Equal(5, triggerP2Pass.State.CardObjects["P2-GHOSTLY-CENTAUR"].Power);
        Assert.Contains("P1-FRIENDLY-TARGET", triggerP2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-SPELL-VENGEANCE", triggerP2Pass.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(triggerP2Pass.State, "P1-FRIENDLY-TARGET", "GRAVEYARD");
    }

    [Fact]
    public async Task P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 8) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-RESONANT-SOUL-DRAW-001", "P1-RESONANT-SOUL-DRAW-002"],
                    Hand = ["P1-SPELL-VENGEANCE-1", "P1-SPELL-VENGEANCE-2"],
                    Base =
                    [
                        "P1-RESONANT-SOUL",
                        "P1-RESONANT-SOUL-FACEDOWN",
                        "P1-RESONANT-SOUL-STANDBY",
                        "P1-FRIENDLY-TARGET-1",
                        "P1-FRIENDLY-TARGET-2"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-RESONANT-SOUL-DRAW-001"],
                    Base = ["P2-RESONANT-SOUL"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RESONANT-SOUL"] = new(
                    "P1-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RESONANT-SOUL-FACEDOWN"] = new(
                    "P1-RESONANT-SOUL-FACEDOWN",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    isFaceDown: true,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RESONANT-SOUL-STANDBY"] = new(
                    "P1-RESONANT-SOUL-STANDBY",
                    isFaceDown: true,
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-TARGET-1"] = new(
                    "P1-FRIENDLY-TARGET-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-TARGET-2"] = new(
                    "P1-FRIENDLY-TARGET-2",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-RESONANT-SOUL"] = new(
                    "P2-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298", "OGN·229/298");

        var firstPlay = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-resonant-soul-play-vengeance-1", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE-1", "OGN·229/298", ["P1-FRIENDLY-TARGET-1"]),
            CancellationToken.None);
        var firstP1Pass = await engine.ResolveAsync(
            firstPlay.State,
            new PlayerIntent("intent-p7-9-resonant-soul-p1-pass-1", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var firstP2Pass = await engine.ResolveAsync(
            firstP1Pass.State,
            new PlayerIntent("intent-p7-9-resonant-soul-p2-pass-1", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(firstPlay.Accepted, firstPlay.ErrorMessage);
        Assert.True(firstP1Pass.Accepted, firstP1Pass.ErrorMessage);
        Assert.True(firstP2Pass.Accepted, firstP2Pass.ErrorMessage);
        var queuedEvent = Assert.Single(firstP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals((gameEvent.Payload.GetValueOrDefault("observerKind") ?? gameEvent.Payload["effectKind"]) as string, "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1", StringComparison.Ordinal));
        Assert.Equal("P1", queuedEvent.Payload["controllerId"]);
        Assert.Equal("P1-RESONANT-SOUL", queuedEvent.Payload["sourceObjectId"]);
        Assert.Contains(firstP2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));
        Assert.DoesNotContain(firstP2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Empty(firstP2Pass.State.TriggerQueue);
        var resonantTriggerStackItem = Assert.Single(firstP2Pass.State.StackItems);
        Assert.Equal("P1", resonantTriggerStackItem.ControllerId);
        Assert.Equal("P1-RESONANT-SOUL", resonantTriggerStackItem.SourceObjectId);
        Assert.Equal("RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1", resonantTriggerStackItem.DeathObserver!.Kind);
        Assert.Equal("P1", firstP2Pass.State.PriorityPlayerId);
        Assert.Equal(["P1-SPELL-VENGEANCE-2"], firstP2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001", "P1-RESONANT-SOUL-DRAW-002"], firstP2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-RESONANT-SOUL-DRAW-001"], firstP2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Contains("P1", firstP2Pass.State.DestroyedUnitOwnerIdsThisTurn);

        var firstTriggerP1Pass = await engine.ResolveAsync(
            firstP2Pass.State,
            new PlayerIntent("intent-p7-9-resonant-soul-trigger-p1-pass-1", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var firstTriggerP2Pass = await engine.ResolveAsync(
            firstTriggerP1Pass.State,
            new PlayerIntent("intent-p7-9-resonant-soul-trigger-p2-pass-1", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(firstTriggerP1Pass.Accepted, firstTriggerP1Pass.ErrorMessage);
        Assert.True(firstTriggerP2Pass.Accepted, firstTriggerP2Pass.ErrorMessage);
        var triggerEvent = Assert.Single(firstTriggerP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals((gameEvent.Payload.GetValueOrDefault("observerKind") ?? gameEvent.Payload["effectKind"]) as string, "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1", StringComparison.Ordinal));
        Assert.Equal("P1", triggerEvent.Payload["controllerId"]);
        Assert.Equal("P1-RESONANT-SOUL", triggerEvent.Payload["sourceObjectId"]);
        var firstDrawEvent = Assert.Single(firstTriggerP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", firstDrawEvent.Payload["playerId"]);
        Assert.Equal(1, firstDrawEvent.Payload["count"]);
        Assert.Empty(firstTriggerP2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-VENGEANCE-2", "P1-RESONANT-SOUL-DRAW-001"], firstTriggerP2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-002"], firstTriggerP2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-RESONANT-SOUL-DRAW-001"], firstTriggerP2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Contains("P1", firstTriggerP2Pass.State.DestroyedUnitOwnerIdsThisTurn);

        var secondPlay = await engine.ResolveAsync(
            firstTriggerP2Pass.State,
            new PlayerIntent("intent-p7-9-resonant-soul-play-vengeance-2", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE-2", "OGN·229/298", ["P1-FRIENDLY-TARGET-2"]),
            CancellationToken.None);
        var secondP1Pass = await engine.ResolveAsync(
            secondPlay.State,
            new PlayerIntent("intent-p7-9-resonant-soul-p1-pass-2", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var secondP2Pass = await engine.ResolveAsync(
            secondP1Pass.State,
            new PlayerIntent("intent-p7-9-resonant-soul-p2-pass-2", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(secondPlay.Accepted, secondPlay.ErrorMessage);
        Assert.True(secondP1Pass.Accepted, secondP1Pass.ErrorMessage);
        Assert.True(secondP2Pass.Accepted, secondP2Pass.ErrorMessage);
        Assert.DoesNotContain(secondP2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals((gameEvent.Payload.GetValueOrDefault("observerKind") ?? gameEvent.Payload["effectKind"]) as string, "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1", StringComparison.Ordinal));
        Assert.DoesNotContain(secondP2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001"], secondP2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-002"], secondP2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-FRIENDLY-TARGET-1", secondP2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-FRIENDLY-TARGET-2", secondP2Pass.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(secondP2Pass.State, "P1-FRIENDLY-TARGET-1", "GRAVEYARD");
        CardZoneTestAssertions.RetainedOutsidePlay(secondP2Pass.State, "P1-FRIENDLY-TARGET-2", "GRAVEYARD");
    }

    [Theory]
    [InlineData("SFD·036/221", "P1-SAD-PORO")]
    [InlineData("UNL-221/219", "P1-UNL-SAD-PORO")]
    public async Task P79SadPoroDrawsWhenDestroyedWhileIsolated(string cardNo, string sourceObjectId)
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    MainDeck = ["P1-SAD-PORO-DRAW"],
                    Base = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: cardNo,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-p7-9-sad-poro-{sourceObjectId}-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", [sourceObjectId]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent($"intent-p7-9-sad-poro-{sourceObjectId}-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-p7-9-sad-poro-{sourceObjectId}-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SAD_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.Equal(sourceObjectId, triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.DoesNotContain(p2Pass.Events, e => e.Kind is "TRIGGER_RESOLVED" or "CARD_DRAWN");
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.NotNull(Assert.Single(p2Pass.State.StackItems).LastBreathDraw);
        var drawResponse = await engine.ResolveAsync(p2Pass.State,
            new PlayerIntent($"intent-p7-9-sad-poro-{sourceObjectId}-draw-response-1", p2Pass.State.PriorityPlayerId!, "PASS_PRIORITY"), new PassPriorityCommand(), CancellationToken.None);
        Assert.True(drawResponse.Accepted, drawResponse.ErrorMessage);
        Assert.DoesNotContain(drawResponse.Events, e => e.Kind is "TRIGGER_RESOLVED" or "CARD_DRAWN");
        var drawResolved = await engine.ResolveAsync(drawResponse.State,
            new PlayerIntent($"intent-p7-9-sad-poro-{sourceObjectId}-draw-response-2", drawResponse.State.PriorityPlayerId!, "PASS_PRIORITY"), new PassPriorityCommand(), CancellationToken.None);
        Assert.True(drawResolved.Accepted, drawResolved.ErrorMessage);
        Assert.Contains(drawResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SAD_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        var drawnEvent = Assert.Single(drawResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", drawnEvent.Payload["playerId"]);
        Assert.Equal(1, drawnEvent.Payload["count"]);
        Assert.Equal(["P1-SAD-PORO-DRAW"], drawResolved.State.PlayerZones["P1"].Hand);
        Assert.Equal([sourceObjectId, "P1-SPELL-VENGEANCE"], drawResolved.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task P79SadPoroSkipsDrawWhenDestroyedWithAnotherFriendlyUnitAtSameBase()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    MainDeck = ["P1-SAD-PORO-DRAW"],
                    Base =
                    [
                        "P1-SAD-PORO",
                        "P1-SAD-PORO-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SAD-PORO"] = new(
                    "P1-SAD-PORO",
                    cardNo: "SFD·036/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SAD-PORO-ALLY"] = new(
                    "P1-SAD-PORO-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sad-poro-non-isolated-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-SAD-PORO"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-sad-poro-non-isolated-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-sad-poro-non-isolated-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SAD_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(["P1-SAD-PORO-DRAW"], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-SAD-PORO", "P1-SPELL-VENGEANCE"], p2Pass.State.PlayerZones["P1"].Graveyard);
    }

    [Theory]
    [InlineData("OGN·216/298", "P1-SCOUTING-WARHAWK", "P1-SCOUTING-WARHAWK-RUNE", 1, "鸟类")]
    [InlineData("UNL-152/219", "P1-BLACK-ROSE-AGENT", "P1-BLACK-ROSE-AGENT-RUNE", 2, "强攻")]
    public async Task P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed(
        string sourceCardNo,
        string sourceObjectId,
        string runeObjectId,
        int sourcePower,
        string sourceTag)
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    RuneDeck = [runeObjectId],
                    Base = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    power: sourcePower,
                    tags: [CardObjectTags.UnitCard, sourceTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [runeObjectId] = new(
                    runeObjectId,
                    ownerId: "P1",
                    controllerId: "P1",
                    tags: [CardObjectTags.RuneCard])
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-scouting-warhawk-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", [sourceObjectId]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-scouting-warhawk-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-scouting-warhawk-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", StringComparison.Ordinal));
        Assert.Equal(sourceObjectId, triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.Single(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, e => e.Kind == "RUNES_CALLED");
        var resolved = await OfficialGraveyardRecastTests.Top(p2Pass.State);
        Assert.Contains(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", StringComparison.Ordinal));

        var runeEvent = Assert.Single(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P1", runeEvent.Payload["playerId"]);
        Assert.Equal(sourceObjectId, runeEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, runeEvent.Payload["count"]);
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", runeEvent.Payload["reason"]);
        Assert.Equal(
            [runeObjectId],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(runeEvent.Payload["runeObjectIds"]));
        Assert.Empty(resolved.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal([runeObjectId], resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal([sourceObjectId, "P1-SPELL-VENGEANCE"], resolved.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(resolved.State, sourceObjectId, "GRAVEYARD");
    }

    [Fact]
    public async Task P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base = ["P1-MECHANICAL-TRICKSTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MECHANICAL-TRICKSTER"] = new(
                    "P1-MECHANICAL-TRICKSTER",
                    cardNo: "OGN·239/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-mechanical-trickster-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-MECHANICAL-TRICKSTER"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-mechanical-trickster-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-mechanical-trickster-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", StringComparison.Ordinal));
        Assert.Equal("P1-MECHANICAL-TRICKSTER", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.TriggerQueue);
        var triggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1", triggerStackItem.ControllerId);
        Assert.Equal("P1-MECHANICAL-TRICKSTER", triggerStackItem.SourceObjectId);
        Assert.Equal("MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", triggerStackItem.EffectKind);
        Assert.Equal("P1", p2Pass.State.PriorityPlayerId);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);

        var p1TriggerPass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-mechanical-trickster-p1-trigger-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesTrigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-p7-9-mechanical-trickster-p2-resolves-trigger", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1TriggerPass.Accepted, p1TriggerPass.ErrorMessage);
        Assert.True(p2ResolvesTrigger.Accepted, p2ResolvesTrigger.ErrorMessage);
        Assert.Contains(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", StringComparison.Ordinal));
        Assert.Equal(3, p2ResolvesTrigger.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.Equal(
            [
                "P1-MECHANICAL-TRICKSTER-TOKEN-001",
                "P1-MECHANICAL-TRICKSTER-TOKEN-002",
                "P1-MECHANICAL-TRICKSTER-TOKEN-003"
            ],
            p2ResolvesTrigger.State.PlayerZones["P1"].Base);
        foreach (var tokenObjectId in p2ResolvesTrigger.State.PlayerZones["P1"].Base)
        {
            Assert.Equal(1, p2ResolvesTrigger.State.CardObjects[tokenObjectId].Power);
            Assert.Equal(
                [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
                p2ResolvesTrigger.State.CardObjects[tokenObjectId].Tags);
        }

        Assert.Empty(p2ResolvesTrigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesTrigger.State.StackItems);
        Assert.Equal(["P1-MECHANICAL-TRICKSTER", "P1-SPELL-VENGEANCE"], p2ResolvesTrigger.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(p2ResolvesTrigger.State, "P1-MECHANICAL-TRICKSTER", "GRAVEYARD");
    }

    [Fact]
    public async Task P79IroncladVanguardCreatesTwoRobotsWhenDestroyed()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base = ["P1-IRONCLAD-VANGUARD"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-IRONCLAD-VANGUARD"] = new(
                    "P1-IRONCLAD-VANGUARD",
                    cardNo: "SFD·021/221",
                    power: 6,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ironclad-vanguard-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-IRONCLAD-VANGUARD"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-ironclad-vanguard-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-ironclad-vanguard-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", StringComparison.Ordinal));
        Assert.Equal("P1-IRONCLAD-VANGUARD", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.TriggerQueue);
        var triggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1", triggerStackItem.ControllerId);
        Assert.Equal("P1-IRONCLAD-VANGUARD", triggerStackItem.SourceObjectId);
        Assert.Equal("IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", triggerStackItem.EffectKind);
        Assert.Equal("P1", p2Pass.State.PriorityPlayerId);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);

        var p1TriggerPass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-ironclad-vanguard-p1-trigger-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesTrigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-p7-9-ironclad-vanguard-p2-resolves-trigger", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1TriggerPass.Accepted, p1TriggerPass.ErrorMessage);
        Assert.True(p2ResolvesTrigger.Accepted, p2ResolvesTrigger.ErrorMessage);
        Assert.Contains(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", StringComparison.Ordinal));
        var tokenEvents = p2ResolvesTrigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, tokenEvents.Length);
        Assert.All(tokenEvents, gameEvent =>
        {
            Assert.Equal("P1-IRONCLAD-VANGUARD", gameEvent.Payload["sourceObjectId"]);
            Assert.Equal("机器人", gameEvent.Payload["tokenName"]);
            Assert.Equal(3, gameEvent.Payload["power"]);
            Assert.Equal("BASE", gameEvent.Payload["destinationZone"]);
        });
        Assert.Equal(
            [
                "P1-IRONCLAD-VANGUARD-TOKEN-001",
                "P1-IRONCLAD-VANGUARD-TOKEN-002"
            ],
            p2ResolvesTrigger.State.PlayerZones["P1"].Base);
        foreach (var tokenObjectId in p2ResolvesTrigger.State.PlayerZones["P1"].Base)
        {
            Assert.Equal(3, p2ResolvesTrigger.State.CardObjects[tokenObjectId].Power);
            Assert.Equal([CardObjectTags.UnitCard, "机械"], p2ResolvesTrigger.State.CardObjects[tokenObjectId].Tags);
        }

        Assert.Empty(p2ResolvesTrigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesTrigger.State.StackItems);
        Assert.Equal(["P1-IRONCLAD-VANGUARD", "P1-SPELL-VENGEANCE"], p2ResolvesTrigger.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(p2ResolvesTrigger.State, "P1-IRONCLAD-VANGUARD", "GRAVEYARD");
    }

    [Fact]
    public async Task P79HonestBrokerCreatesSleepingGoldWhenDestroyed()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base = ["P1-HONEST-BROKER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HONEST-BROKER"] = new(
                    "P1-HONEST-BROKER",
                    cardNo: "SFD·155/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-honest-broker-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-HONEST-BROKER"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-honest-broker-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-honest-broker-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HONEST_BROKER_LAST_BREATH_CREATE_GOLD", StringComparison.Ordinal));
        Assert.Equal("P1-HONEST-BROKER", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.Single(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        var resolved = await OfficialGraveyardRecastTests.Top(p2Pass.State);
        Assert.Contains(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HONEST_BROKER_LAST_BREATH_CREATE_GOLD", StringComparison.Ordinal));

        var tokenEvent = Assert.Single(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P1", tokenEvent.Payload["playerId"]);
        Assert.Equal("P1-HONEST-BROKER", tokenEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-HONEST-BROKER-TOKEN-001", tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("金币", tokenEvent.Payload["tokenName"]);
        Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        Assert.Equal(true, tokenEvent.Payload["isExhausted"]);
        Assert.Equal([CardObjectTags.EquipmentCard, "反应", "金币"], Assert.IsAssignableFrom<IReadOnlyList<string>>(tokenEvent.Payload["tokenTags"]));

        Assert.Equal(["P1-HONEST-BROKER-TOKEN-001"], resolved.State.PlayerZones["P1"].Base);
        var goldToken = resolved.State.CardObjects["P1-HONEST-BROKER-TOKEN-001"];
        Assert.True(goldToken.IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard, "反应", "金币"], goldToken.Tags);
        Assert.Equal(["P1-HONEST-BROKER", "P1-SPELL-VENGEANCE"], resolved.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(resolved.State, "P1-HONEST-BROKER", "GRAVEYARD");
    }

    [Fact]
    public async Task P79UnsungHeroDrawsTwoWhenDestroyedWhilePowerful()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"],
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base = ["P1-UNSUNG-HERO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNSUNG-HERO"] = new(
                    "P1-UNSUNG-HERO",
                    cardNo: "SFD·167/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "精锐"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-unsung-hero-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-UNSUNG-HERO"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-unsung-hero-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-unsung-hero-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2", StringComparison.Ordinal));
        Assert.Equal("P1-UNSUNG-HERO", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal("UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2", Assert.Single(p2Pass.State.StackItems).EffectKind);
        var response1 = await engine.ResolveAsync(p2Pass.State,
            new("intent-p7-9-unsung-hero-trigger-p1-pass", "P1", "PASS_PRIORITY"), new PassPriorityCommand(), default);
        var response2 = await engine.ResolveAsync(response1.State,
            new("intent-p7-9-unsung-hero-trigger-p2-pass", "P2", "PASS_PRIORITY"), new PassPriorityCommand(), default);
        Assert.True(response1.Accepted, response1.ErrorMessage); Assert.True(response2.Accepted, response2.ErrorMessage);
        Assert.Contains(response2.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2", StringComparison.Ordinal));

        var drawEvent = Assert.Single(response2.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", drawEvent.Payload["playerId"]);
        Assert.Equal(2, drawEvent.Payload["count"]);
        Assert.Equal(["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"], response2.State.PlayerZones["P1"].Hand);
        Assert.Empty(response2.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNSUNG-HERO", "P1-SPELL-VENGEANCE"], response2.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(response2.State, "P1-UNSUNG-HERO", "GRAVEYARD");
    }

    [Fact]
    public async Task P79UnsungHeroSkipsDrawWhenDestroyedBelowPowerful()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"],
                    Hand = ["P1-SPELL-VENGEANCE"],
                    Base = ["P1-UNSUNG-HERO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNSUNG-HERO"] = new(
                    "P1-UNSUNG-HERO",
                    cardNo: "SFD·167/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "精锐"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-unsung-hero-below-powerful-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-UNSUNG-HERO"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-unsung-hero-below-powerful-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-unsung-hero-below-powerful-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNSUNG-HERO", "P1-SPELL-VENGEANCE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(p2Pass.State, "P1-UNSUNG-HERO", "GRAVEYARD");
    }

    [Fact]
    public async Task P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    MainDeck = ["P1-LOYAL-PORO-DRAW"],
                    Base =
                    [
                        "P1-LOYAL-PORO",
                        "P1-LOYAL-PORO-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LOYAL-PORO"] = new(
                    "P1-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "魄罗"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LOYAL-PORO-ALLY"] = new(
                    "P1-LOYAL-PORO-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-loyal-poro-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-LOYAL-PORO"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-loyal-poro-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-loyal-poro-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "LOYAL_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.Equal("P1-LOYAL-PORO", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1", triggerQueued.Payload["controllerId"]);
        Assert.Equal("UNIT_DESTROYED", triggerQueued.Payload["triggeredByEventKind"]);
        Assert.DoesNotContain(p2Pass.Events, e => e.Kind is "TRIGGER_RESOLVED" or "CARD_DRAWN");
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.NotNull(Assert.Single(p2Pass.State.StackItems).LastBreathDraw);
        var drawResponse = await engine.ResolveAsync(p2Pass.State,
            new PlayerIntent("intent-p7-9-loyal-poro-draw-response-1", p2Pass.State.PriorityPlayerId!, "PASS_PRIORITY"), new PassPriorityCommand(), CancellationToken.None);
        Assert.True(drawResponse.Accepted, drawResponse.ErrorMessage);
        Assert.DoesNotContain(drawResponse.Events, e => e.Kind is "TRIGGER_RESOLVED" or "CARD_DRAWN");
        var drawResolved = await engine.ResolveAsync(drawResponse.State,
            new PlayerIntent("intent-p7-9-loyal-poro-draw-response-2", drawResponse.State.PriorityPlayerId!, "PASS_PRIORITY"), new PassPriorityCommand(), CancellationToken.None);
        Assert.True(drawResolved.Accepted, drawResolved.ErrorMessage);
        var triggerResolved = Assert.Single(drawResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "LOYAL_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.Equal("P1-LOYAL-PORO", triggerResolved.Payload["sourceObjectId"]);

        var drawnEvent = Assert.Single(drawResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", drawnEvent.Payload["playerId"]);
        Assert.Equal(1, drawnEvent.Payload["count"]);
        Assert.Equal(["P1-LOYAL-PORO-ALLY"], drawResolved.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-LOYAL-PORO-DRAW"], drawResolved.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LOYAL-PORO", "P1-SPELL-VENGEANCE"], drawResolved.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(drawResolved.State, "P1-LOYAL-PORO", "GRAVEYARD");
    }

    [Fact]
    public async Task P79LoyalPoroSkipsDrawWhenDestroyedWhileIsolated()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"],
                    MainDeck = ["P1-LOYAL-PORO-DRAW"],
                    Base = ["P1-LOYAL-PORO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LOYAL-PORO"] = new(
                    "P1-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "魄罗"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-loyal-poro-isolated-play-vengeance", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P1-LOYAL-PORO"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-loyal-poro-isolated-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-loyal-poro-isolated-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "LOYAL_PORO_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LOYAL-PORO-DRAW"], p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-LOYAL-PORO", "P1-SPELL-VENGEANCE"], p2Pass.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-SCARLET-PIGEON", "P1-ALLY-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SCARLET-PIGEON"] = new(
                    "P1-SCARLET-PIGEON",
                    cardNo: "UNL-154/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "鸟类"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ALLY-ATTACKER"] = new(
                    "P1-ALLY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 8,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-scarlet-pigeon-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-SCARLET-PIGEON", "P1-ALLY-ATTACKER"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var pigeonDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SCARLET-PIGEON", StringComparison.Ordinal));
        Assert.Equal("P2-DEFENDER", pigeonDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(3, pigeonDamageEvent.Payload["basePower"]);
        Assert.Equal(0, pigeonDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, pigeonDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, pigeonDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, pigeonDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79ScarletPigeonSkipsSourceCombatPowerWhenSourceIsStandby()
    {
        // 811.5: a face-up keyword does not suppress the source combat ability.
        var state = WithStandbyTag(
            PunishmentState(mana: 0) with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
                {
                    ["P1"] = PlayerZones.Empty with
                    {
                        Battlefields = ["P1-SCARLET-PIGEON", "P1-ALLY-ATTACKER"]
                    },
                    ["P2"] = PlayerZones.Empty with
                    {
                        Battlefields = ["P2-DEFENDER"]
                    }
                },
                CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    ["P1-SCARLET-PIGEON"] = new(
                        "P1-SCARLET-PIGEON",
                        cardNo: "UNL-154/219",
                        power: 3,
                        tags: [CardObjectTags.UnitCard, "鸟类"],
                        ownerId: "P1",
                        controllerId: "P1"),
                    ["P1-ALLY-ATTACKER"] = new(
                        "P1-ALLY-ATTACKER",
                        cardNo: "SFD·125/221",
                        power: 1,
                        tags: [CardObjectTags.UnitCard],
                        ownerId: "P1",
                        controllerId: "P1"),
                    ["P2-DEFENDER"] = new(
                        "P2-DEFENDER",
                        cardNo: "SFD·125/221",
                        power: 8,
                        tags: [CardObjectTags.UnitCard],
                        ownerId: "P2",
                        controllerId: "P2")
                }
            },
            "P1-SCARLET-PIGEON");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-scarlet-pigeon-standby-source-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-SCARLET-PIGEON", "P1-ALLY-ATTACKER"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var pigeonDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SCARLET-PIGEON", StringComparison.Ordinal));
        Assert.Equal(3, pigeonDamageEvent.Payload["basePower"]);
        Assert.Equal(2, pigeonDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, pigeonDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79SharpshooterPirateDamagesEnemyUnitWhenAttackingBattlefield()
    {
        var state = SharpshooterPirateBattleState(sharpshooterAttacks: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sharpshooter-pirate-attack-trigger", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-SHARPSHOOTER-PIRATE"],
                ["P2-SHARPSHOOTER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("effectKind", out var effectKind)
            && string.Equals(effectKind as string, "SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1", StringComparison.Ordinal));
        Assert.Equal("P1-SHARPSHOOTER-PIRATE", triggerEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-SHARPSHOOTER-DEFENDER", triggerEvent.Payload["targetObjectId"]);
        Assert.Equal(1, triggerEvent.Payload["damageAmount"]);

        var triggerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("effectKind", out var effectKind)
            && string.Equals(effectKind as string, "SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1", StringComparison.Ordinal));
        Assert.Equal("P1-SHARPSHOOTER-PIRATE", triggerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-SHARPSHOOTER-DEFENDER", triggerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(1, triggerDamageEvent.Payload["damage"]);
        Assert.Equal("ATTACK_TRIGGER", triggerDamageEvent.Payload["reason"]);

        var combatDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SHARPSHOOTER-PIRATE", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P2-SHARPSHOOTER-DEFENDER", combatDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(3, combatDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, combatDamageEvent.Payload["damage"]);
        var removedDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(removedDamageEvent.Payload["previousDamageByObject"]);
        Assert.Equal(4, previousDamageByObject["P2-SHARPSHOOTER-DEFENDER"]);
    }

    [Fact]
    public async Task P79SharpshooterPirateSkipsAttackDamageWhenDefending()
    {
        var state = SharpshooterPirateBattleState(sharpshooterAttacks: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sharpshooter-pirate-defending-no-trigger", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-SHARPSHOOTER-ATTACKER"],
                ["P2-SHARPSHOOTER-PIRATE"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("effectKind", out var effectKind)
            && string.Equals(effectKind as string, "SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("effectKind", out var effectKind)
            && string.Equals(effectKind as string, "SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79DuneDrakeGainsPowerWhenAttackingReadyEnemyUnit()
    {
        var state = DuneDrakeBattleState(duneDrakeAttacks: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-dune-drake-ready-enemy-static", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-DUNE-DRAKE"],
                ["P2-DUNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var duneDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-DUNE-DRAKE", StringComparison.Ordinal));
        Assert.Equal("P2-DUNE-DEFENDER", duneDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(5, duneDamageEvent.Payload["basePower"]);
        Assert.Equal(0, duneDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, duneDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(7, duneDamageEvent.Payload["combatPower"]);
        Assert.Equal(7, duneDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79DuneDrakeSkipsSourceCombatPowerWhenSourceIsStandby()
    {
        // 811.5: a face-up keyword does not suppress the source combat ability.
        var state = WithStandbyTag(DuneDrakeBattleState(duneDrakeAttacks: true), "P1-DUNE-DRAKE");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-dune-drake-standby-source-static", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-DUNE-DRAKE"],
                ["P2-DUNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var duneDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-DUNE-DRAKE", StringComparison.Ordinal));
        Assert.Equal(5, duneDamageEvent.Payload["basePower"]);
        Assert.Equal(2, duneDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(7, duneDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79DuneDrakeSkipsPowerWhenDefending()
    {
        var state = DuneDrakeBattleState(duneDrakeAttacks: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-dune-drake-defending-no-static", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-DUNE-ATTACKER"],
                ["P2-DUNE-DRAKE"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var duneDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-DUNE-DRAKE", StringComparison.Ordinal));
        Assert.Equal("P1-DUNE-ATTACKER", duneDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(5, duneDamageEvent.Payload["basePower"]);
        Assert.Equal(0, duneDamageEvent.Payload["keywordBonus"]);
        Assert.False(duneDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, duneDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, duneDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticRoamAllowsPreciseBattlefieldMovement()
    {
        var state = BattlefieldStaticRoamState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-static-roam", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-WIND-RUNNER",
                "BATTLEFIELD:P1-BATTLEFIELD-WIND-HILL",
                "BATTLEFIELD:P1-FAR-FIELD",
                ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(new[] {"UNIT_MOVED_TO_BATTLEFIELD", "BATTLEFIELD_CONTROL_LOST"}, result.Events.Select(e => e.Kind));
        var moveEvent = Assert.Single(result.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", moveEvent.Kind);
        Assert.Equal("游走", moveEvent.Payload["movementKeyword"]);
        Assert.Equal("BATTLEFIELD:P1-BATTLEFIELD-WIND-HILL", moveEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD:P1-FAR-FIELD", moveEvent.Payload["destination"]);
        Assert.Equal(["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("游走", result.State.CardObjects["P1-BATTLEFIELD-WIND-RUNNER"].Tags);
        Assert.DoesNotContain("ROAM", result.State.CardObjects["P1-BATTLEFIELD-WIND-RUNNER"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task P79BilgewaterBullyWithBoonCanUseRoam()
    {
        var state = BilgewaterBullyBoonRoamState(hasBoon: true);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        Assert.Contains(sourceRequirements, requirement =>
            string.Equals(requirement["sourceObjectId"] as string, "P1-BILGEWATER-BULLY", StringComparison.Ordinal)
            && string.Equals(requirement["mode"] as string, "ROAM", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-bilgewater-bully-boon-roam", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BILGEWATER-BULLY",
                "BATTLEFIELD:P1-BILGEWATER-DOCK",
                "BATTLEFIELD:P1-MAIN",
                ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new[] {"UNIT_MOVED_TO_BATTLEFIELD", "BATTLEFIELD_CONTROL_LOST"}, result.Events.Select(e => e.Kind));
        var moveEvent = Assert.Single(result.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", moveEvent.Kind);
        Assert.Equal("游走", moveEvent.Payload["movementKeyword"]);
        Assert.Equal("BATTLEFIELD:P1-BILGEWATER-DOCK", moveEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD:P1-MAIN", moveEvent.Payload["destination"]);
        var bully = result.State.CardObjects["P1-BILGEWATER-BULLY"];
        Assert.Contains(CardObjectTags.Boon, bully.Tags);
        Assert.DoesNotContain("游走", bully.Tags);
        Assert.Equal("P1-MAIN", result.State.ObjectLocations["P1-BILGEWATER-BULLY"].BattlefieldObjectId);
    }

    [Fact]
    public async Task P79BilgewaterBullyWithoutBoonDoesNotUseRoam()
    {
        var state = BilgewaterBullyBoonRoamState(hasBoon: false);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        Assert.DoesNotContain(sourceRequirements, requirement =>
            string.Equals(requirement["sourceObjectId"] as string, "P1-BILGEWATER-BULLY", StringComparison.Ordinal)
            && string.Equals(requirement["mode"] as string, "ROAM", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-bilgewater-bully-no-boon-roam", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BILGEWATER-BULLY",
                "BATTLEFIELD:P1-BILGEWATER-DOCK",
                "BATTLEFIELD:P1-MAIN",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("精确战场移动需要游走许可。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal("P1-BILGEWATER-DOCK", result.State.ObjectLocations["P1-BILGEWATER-BULLY"].BattlefieldObjectId);
    }

    [Fact]
    public void P79RudePiratePromptOffersDiscardCostReduction()
    {
        var state = RudePirateOptionalDiscardState(mana: 4);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-UNIT-RUDE-PIRATE", StringComparison.Ordinal));

        Assert.Equal(4, sourceRequirement["minimumManaCost"]);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        var discardChoice = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "DISCARD_HAND_CARD:P1-RUDE-PIRATE-DISCARD-001", StringComparison.Ordinal));
        Assert.Contains("费用减少 2", discardChoice.Label, StringComparison.Ordinal);
        Assert.DoesNotContain(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "DISCARD_HAND_CARD:P1-UNIT-RUDE-PIRATE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79RudePirateDiscardOptionalCostReducesManaAndDiscards()
    {
        var state = RudePirateOptionalDiscardState(mana: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rude-pirate-discard-cost-reduction", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-RUDE-PIRATE",
                "OGN·002/298",
                [],
                OptionalCosts: ["DISCARD_HAND_CARD:P1-RUDE-PIRATE-DISCARD-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUDE-PIRATE-DISCARD-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Contains("P1-UNIT-RUDE-PIRATE", result.State.PlayerZones["P1"].Base);
        Assert.Contains("DISCARDED_HAND_CARD_THIS_TURN:P1", result.State.UntilEndOfTurnEffects);

        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(4, costPaid.Payload["mana"]);
        Assert.Equal(6, costPaid.Payload["baseMana"]);
        Assert.Equal(2, costPaid.Payload["optionalCostManaReduction"]);
        Assert.Equal(
            ["DISCARD_HAND_CARD:P1-RUDE-PIRATE-DISCARD-001"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(costPaid.Payload["optionalCosts"]));

        var discardEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
        Assert.Equal("P1", discardEvent.Payload["playerId"]);
        Assert.Equal("P1-UNIT-RUDE-PIRATE", discardEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-RUDE-PIRATE-DISCARD-001", discardEvent.Payload["targetObjectId"]);
        Assert.Equal("OPTIONAL_COST", discardEvent.Payload["reason"]);
        Assert.Equal("GRAVEYARD", discardEvent.Payload["destinationZone"]);
    }

    [Fact]
    public async Task P79RudePirateRejectsDiscardingSourceAsOptionalCost()
    {
        var state = RudePirateOptionalDiscardState(mana: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rude-pirate-self-discard-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-RUDE-PIRATE",
                "OGN·002/298",
                [],
                OptionalCosts: ["DISCARD_HAND_CARD:P1-UNIT-RUDE-PIRATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-RUDE-PIRATE", "P1-RUDE-PIRATE-DISCARD-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79RagingDrakeCreatesNextSpellCostReductionAfterResolution()
    {
        var state = RagingDrakePlayState();
        state = PrintedCostFixture.Add(state, "P1", "OGN·031/298");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-raging-drake-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-RAGING-DRAKE", "OGN·031/298", []),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-raging-drake-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-raging-drake-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), play.State.RunePools["P1"]);
        Assert.Contains("P1-UNIT-RAGING-DRAKE", p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(
            "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:P1-UNIT-RAGING-DRAKE",
            p2Pass.State.UntilEndOfTurnEffects);

        var drake = p2Pass.State.CardObjects["P1-UNIT-RAGING-DRAKE"];
        Assert.Equal(4, drake.Power);
        Assert.Contains(CardObjectTags.UnitCard, drake.Tags);
        Assert.Contains("龙", drake.Tags);

        var triggerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-RAGING-DRAKE", triggerEvent.Payload["sourceObjectId"]);
        Assert.Equal(5, triggerEvent.Payload["amount"]);
    }

    [Fact]
    public void P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost()
    {
        var state = RagingDrakeNextSpellCostReductionState(mana: 0);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-PUNISHMENT", StringComparison.Ordinal));

        Assert.Equal(0, sourceRequirement["minimumManaCost"]);
        Assert.Equal(2, sourceRequirement["nextSpellCostReductionMana"]);
        Assert.Equal(0, sourceRequirement["battlefieldSpellCostReductionMana"]);
    }

    [Fact]
    public async Task P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker()
    {
        var state = RagingDrakeNextSpellCostReductionState(mana: 0);
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-raging-drake-next-spell-reduced", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(
            "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:P1-UNIT-RAGING-DRAKE",
            result.State.UntilEndOfTurnEffects);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-PUNISHMENT", stackItem.SourceObjectId);

        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Equal(2, costPaid.Payload["nextSpellCostReductionMana"]);

        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION", StringComparison.Ordinal));
        Assert.Equal("UNL-007/219", triggerEvent.Payload["playedCardNo"]);
        Assert.Equal(2, triggerEvent.Payload["costReductionMana"]);
    }

    [Fact]
    public async Task P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro()
    {
        var state = PoroHerderWithPoroState();
        state = PrintedCostFixture.Add(state, "P1", "OGN·061/298");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-poro-herder-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PORO-HERDER", "OGN·061/298", []),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-poro-herder-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-poro-herder-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), play.State.RunePools["P1"]);
        Assert.Equal(["P1-PORO-HERDER-DRAW"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-PLUCKY-PORO", "P1-UNIT-PORO-HERDER"], p2Pass.State.PlayerZones["P1"].Base);

        var herder = p2Pass.State.CardObjects["P1-UNIT-PORO-HERDER"];
        Assert.Equal(4, herder.Power);
        Assert.Contains(CardObjectTags.Boon, herder.Tags);
        Assert.Contains(CardObjectTags.UnitCard, herder.Tags);

        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "PORO_HERDER_BOON_DRAW", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-PORO-HERDER", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79MountainApeElderReadiesWhenGrantedBoon()
    {
        var state = MountainApeElderBoonReadyState(alreadyHasBoon: false);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-mountain-ape-boon-ready-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SECRET-ART-MERCY",
                "OGN·053/298",
                ["P1-UNIT-MOUNTAIN-APE-ELDER"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-mountain-ape-boon-ready-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-mountain-ape-boon-ready-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var mountainApe = p2Pass.State.CardObjects["P1-UNIT-MOUNTAIN-APE-ELDER"];
        Assert.False(mountainApe.IsExhausted);
        Assert.Equal(6, mountainApe.Power);
        Assert.Contains(CardObjectTags.Boon, mountainApe.Tags);

        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-MOUNTAIN-APE-ELDER", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.UnitBoonGrantedReadySelf, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, TriggerKinds.UnitBoonGrantedReadySelf, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["grantingSourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal));
        var readyEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, TriggerKinds.UnitBoonGrantedReadySelf, StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-MOUNTAIN-APE-ELDER", readyEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-UNIT-MOUNTAIN-APE-ELDER", readyEvent.Payload["targetObjectId"]);
        Assert.Equal(true, readyEvent.Payload["wasExhausted"]);
    }

    [Fact]
    public async Task P79MountainApeElderSkipsReadyWhenBoonAlreadyPresent()
    {
        var state = MountainApeElderBoonReadyState(alreadyHasBoon: true);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-mountain-ape-existing-boon-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SECRET-ART-MERCY",
                "OGN·053/298",
                ["P1-UNIT-MOUNTAIN-APE-ELDER"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-mountain-ape-existing-boon-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-mountain-ape-existing-boon-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var mountainApe = p2Pass.State.CardObjects["P1-UNIT-MOUNTAIN-APE-ELDER"];
        Assert.True(mountainApe.IsExhausted);
        Assert.Equal(6, mountainApe.Power);
        Assert.Contains(CardObjectTags.Boon, mountainApe.Tags);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-MOUNTAIN-APE-ELDER", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, TriggerKinds.UnitBoonGrantedReadySelf, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("READY_LEGEND", true, false, "UNIT_READIED")]
    [InlineData("EXHAUST_LEGEND", false, true, "UNIT_EXHAUSTED")]
    public async Task P79RoyalAttendantReadiesOrExhaustsTargetLegend(
        string mode,
        bool initialLegendExhausted,
        bool expectedLegendExhausted,
        string expectedEventKind)
    {
        var state = RoyalAttendantLegendModeState(initialLegendExhausted);
        state = PrintedCostFixture.Add(state, "P1", "SFD·039/221");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-royal-attendant-{mode}-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ROYAL-ATTENDANT",
                "SFD·039/221",
                ["P1-LEGEND-001"],
                Mode: mode),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent($"intent-royal-attendant-{mode}-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-royal-attendant-{mode}-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-UNIT-ROYAL-ATTENDANT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.True(p2Pass.State.CardObjects["P1-UNIT-ROYAL-ATTENDANT"].IsExhausted);
        Assert.Equal(4, p2Pass.State.CardObjects["P1-UNIT-ROYAL-ATTENDANT"].Power);
        Assert.Equal(expectedLegendExhausted, p2Pass.State.CardObjects["P1-LEGEND-001"].IsExhausted);
        var legendStateEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, expectedEventKind, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-LEGEND-001", StringComparison.Ordinal));
        Assert.Equal(initialLegendExhausted, legendStateEvent.Payload["wasExhausted"]);
        Assert.Equal(expectedLegendExhausted, legendStateEvent.Payload["isExhausted"]);
    }

    [Fact]
    public async Task P79RoyalAttendantPromptExposesLegendModesAndTargets()
    {
        var state = RoyalAttendantLegendModeState(initialLegendExhausted: true);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        Assert.Equal(["EXHAUST_LEGEND", "READY_LEGEND"], sourceRequirements
            .Select(requirement => Assert.IsType<string>(requirement["mode"]))
            .Order(StringComparer.Ordinal)
            .ToArray());
        foreach (var requirement in sourceRequirements)
        {
            Assert.Equal("SFD·039/221", Assert.IsType<string>(requirement["cardNo"]));
            Assert.Equal("传奇", Assert.IsType<string>(requirement["targetScopeLabel"]));
            Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
            Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));
            var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
                requirement["targetChoicesByIndex"]);
            Assert.Empty(choicesByIndex);
            var entered = await OfficialGraveyardRecastTests.Act(PrintedCostFixture.Add(state, "P1", "SFD·039/221"), "P1", new PlayCardCommand(
                "P1-UNIT-ROYAL-ATTENDANT", "SFD·039/221", [], Mode: (string)requirement["mode"]!));
            var choices = entered.State.PendingCardChoice!.LegalObjectIds.Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(["P1-LEGEND-001", "P2-LEGEND-001"], choices);
        }
    }

    [Theory]
    [InlineData("SFD·058/221", "P1-UNIT-SFD-058-ORNN")]
    [InlineData("SFD·058a/221", "P1-UNIT-SFD-058A-ORNN")]
    public async Task P79OrnnDrawsSelectedTopEquipmentAndRecyclesRest(string cardNo, string sourceObjectId)
    {
        var state = OrnnEquipmentLookState(cardNo, sourceObjectId);
        state = PrintedCostFixture.Add(state, "P1", cardNo);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-ornn-{cardNo}-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                []),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent($"intent-ornn-{cardNo}-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-ornn-{cardNo}-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var choice = Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        Assert.Equal(["P1-ORNN-EQUIPMENT-001", "P1-ORNN-EQUIPMENT-002"], choice.LegalObjectIds);
        p2Pass = await engine.ResolveAsync(p2Pass.State, new("ornn-choice", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-ORNN-EQUIPMENT-001"]), default);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal([sourceObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-ORNN-EQUIPMENT-001", p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-ORNN-DECK-TAIL-001", "P1-ORNN-UNIT-001", "P1-ORNN-SPELL-001", "P1-ORNN-EQUIPMENT-002"],
            p2Pass.State.PlayerZones["P1"].MainDeck);

        var drawEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", drawEvent.Payload["playerId"]);
        Assert.Equal(1, drawEvent.Payload["count"]);
        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("P1", recycleEvent.Payload["playerId"]);
        Assert.Equal(3, recycleEvent.Payload["count"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
    }

    [Theory]
    [InlineData("SFD·058/221", "P1-UNIT-SFD-058-ORNN")]
    [InlineData("SFD·058a/221", "P1-UNIT-SFD-058A-ORNN")]
    public async Task P79OrnnMayDeclineEquipmentAndRecyclesViewedCards(string cardNo, string sourceObjectId)
    {
        var state = OrnnEquipmentLookState(cardNo, sourceObjectId);
        state = PrintedCostFixture.Add(state, "P1", cardNo);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-ornn-{cardNo}-decline-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                []),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent($"intent-ornn-{cardNo}-decline-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-ornn-{cardNo}-decline-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var choice = Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        Assert.Equal(["P1-ORNN-EQUIPMENT-001", "P1-ORNN-EQUIPMENT-002"], choice.LegalObjectIds);
        p2Pass = await engine.ResolveAsync(p2Pass.State, new("ornn-choice", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []), default);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal([sourceObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-ORNN-DECK-TAIL-001", "P1-ORNN-UNIT-001", "P1-ORNN-SPELL-001", "P1-ORNN-EQUIPMENT-001", "P1-ORNN-EQUIPMENT-002"],
            p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("P1", recycleEvent.Payload["playerId"]);
        Assert.Equal(4, recycleEvent.Payload["count"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
    }

    [Fact]
    public async Task P79OrnnRejectsNonEquipmentTopDeckSelection()
    {
        var state = OrnnEquipmentLookState("SFD·058/221", "P1-UNIT-SFD-058-ORNN");
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-invalid-top-unit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SFD-058-ORNN",
                "SFD·058/221",
                ["P1-ORNN-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal(["P1-UNIT-SFD-058-ORNN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-ORNN-EQUIPMENT-001", "P1-ORNN-UNIT-001", "P1-ORNN-SPELL-001", "P1-ORNN-EQUIPMENT-002", "P1-ORNN-DECK-TAIL-001"],
            result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public void P79OrnnPromptExposesTopEquipmentChoicesOnly()
    {
        var state = OrnnEquipmentLookState("SFD·058/221", "P1-UNIT-SFD-058-ORNN");

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));

        Assert.Equal("P1-UNIT-SFD-058-ORNN", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("己方主牌堆牌", Assert.IsType<string>(sourceRequirement["targetScopeLabel"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.DoesNotContain("P1-ORNN-EQUIPMENT-001", JsonSerializer.Serialize(prompt));
    }

    [Fact]
    public async Task P79EclipseVanguardReadiesAndGainsPowerWhenControllerStunsEnemyUnit()
    {
        var state = EclipseVanguardStunTriggerState(stunEnemy: true);
        var played = await OfficialGraveyardRecastTests.Act(state, "P1", new PlayCardCommand(
            "P1-UNIT-SUN-SHIELDGUARD", "OGN·051/298", ["P2-ECLIPSE-STUN-TARGET"]));
        var stunned = await OfficialGraveyardRecastTests.Top(played.State);
        Assert.Contains("STUNNED", stunned.State.CardObjects["P2-ECLIPSE-STUN-TARGET"].UntilEndOfTurnEffects);
        Assert.True(stunned.State.CardObjects["P1-UNIT-ECLIPSE-VANGUARD"].IsExhausted);
        Assert.Equal(7, stunned.State.CardObjects["P1-UNIT-ECLIPSE-VANGUARD"].Power);
        var resolved = await OfficialGraveyardRecastTests.Top(stunned.State);
        var vanguard = resolved.State.CardObjects["P1-UNIT-ECLIPSE-VANGUARD"];
        Assert.False(vanguard.IsExhausted); Assert.Equal(8, vanguard.Power);
        Assert.Equal(1, vanguard.UntilEndOfTurnPowerModifier);
        Assert.Contains(resolved.Events, e => e.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Payload.GetValueOrDefault("effectKind"), "ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1"));
        Assert.Contains(resolved.Events, e => e.Kind == "UNIT_READIED");
        Assert.Contains(resolved.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
    }

    [Fact]
    public async Task P79EclipseVanguardSkipsTriggerWhenControllerStunsFriendlyUnit()
    {
        var state = EclipseVanguardStunTriggerState(stunEnemy: false);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-play-friendly-stunner", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SUN-SHIELDGUARD",
                "OGN·051/298",
                ["P1-FRIENDLY-STUN-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-friendly-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-friendly-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects["P1-FRIENDLY-STUN-TARGET"].UntilEndOfTurnEffects);

        var vanguard = p2Pass.State.CardObjects["P1-UNIT-ECLIPSE-VANGUARD"];
        Assert.True(vanguard.IsExhausted);
        Assert.Equal(7, vanguard.Power);
        Assert.Equal(0, vanguard.UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-ECLIPSE-VANGUARD", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-ECLIPSE-VANGUARD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79EclipseVanguardSkipsTriggerWhenSourceIsStandby()
    {
        var state = WithFaceDownStandby(EclipseVanguardStunTriggerState(stunEnemy: true), "P1-UNIT-ECLIPSE-VANGUARD");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-standby-play-stunner", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SUN-SHIELDGUARD",
                "OGN·051/298",
                ["P2-ECLIPSE-STUN-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-standby-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-eclipse-vanguard-standby-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects["P2-ECLIPSE-STUN-TARGET"].UntilEndOfTurnEffects);

        var vanguard = p2Pass.State.CardObjects["P1-UNIT-ECLIPSE-VANGUARD"];
        Assert.True(vanguard.IsExhausted);
        Assert.Equal(7, vanguard.Power);
        Assert.Equal(0, vanguard.UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-ECLIPSE-VANGUARD", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-ECLIPSE-VANGUARD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79WiseElderWithBoonAddsPowerInBattle()
    {
        var state = WiseElderBoonBattleState(hasBoon: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-wise-elder-boon-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WISE-ELDER"],
                ["P2-WISE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var wiseDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-WISE-ELDER", StringComparison.Ordinal));
        Assert.Equal(4, wiseDamageEvent.Payload["basePower"]);
        Assert.Equal(1, wiseDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, wiseDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, wiseDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79WiseElderWithoutBoonSkipsPowerBonus()
    {
        var state = WiseElderBoonBattleState(hasBoon: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-wise-elder-no-boon-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WISE-ELDER"],
                ["P2-WISE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var wiseDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-WISE-ELDER", StringComparison.Ordinal));
        Assert.Equal(4, wiseDamageEvent.Payload["basePower"]);
        Assert.False(wiseDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(4, wiseDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, wiseDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79WaterbenderGainsPowerWhenAttackingAlone()
    {
        var state = WaterbenderBattleState(isAttacking: true, lone: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-waterbender-lone-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WATERBENDER"],
                ["P2-WATERBENDER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var waterbenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-WATERBENDER", StringComparison.Ordinal));
        Assert.Equal(2, waterbenderDamageEvent.Payload["basePower"]);
        Assert.Equal(2, waterbenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, waterbenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, waterbenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79WaterbenderSkipsSourceCombatPowerWhenSourceIsStandby()
    {
        // 811.5: a face-up keyword does not suppress the source combat ability.
        var state = WithStandbyTag(WaterbenderBattleState(isAttacking: true, lone: true), "P1-WATERBENDER");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-waterbender-standby-source-static", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WATERBENDER"],
                ["P2-WATERBENDER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var waterbenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-WATERBENDER", StringComparison.Ordinal));
        Assert.Equal(2, waterbenderDamageEvent.Payload["basePower"]);
        Assert.Equal(2, waterbenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, waterbenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79WaterbenderSkipsPowerWhenAttackingWithAnotherUnit()
    {
        var state = WaterbenderBattleState(isAttacking: true, lone: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-waterbender-multi-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WATERBENDER", "P1-WATERBENDER-ALLY"],
                ["P2-WATERBENDER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var waterbenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-WATERBENDER", StringComparison.Ordinal));
        Assert.Equal(2, waterbenderDamageEvent.Payload["basePower"]);
        Assert.False(waterbenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, waterbenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, waterbenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79WaterbenderGainsPowerWhenDefendingAlone()
    {
        var state = WaterbenderBattleState(isAttacking: false, lone: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-waterbender-lone-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-WATERBENDER-ATTACKER"],
                ["P2-WATERBENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var waterbenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-WATERBENDER", StringComparison.Ordinal));
        Assert.Equal(2, waterbenderDamageEvent.Payload["basePower"]);
        Assert.Equal(2, waterbenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, waterbenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, waterbenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit()
    {
        var state = BattlefieldStaticPreventMoveBaseState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-static-prevent-move-base", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-TRAPPED-UNIT",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(
            "该战场效果禁止单位从此战场移动回基地。",
            result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["P1-BATTLEFIELD-VILEMAW-LAIR", "P1-BATTLEFIELD-TRAPPED-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P79BattlefieldMovedUnitGainsTemporaryPower()
    {
        var state = BattlefieldMovePowerState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-move-power", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-BAR-REGULAR",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-BATTLEFIELD-BACK-ALLEY-BAR"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-BATTLEFIELD-BAR-REGULAR"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(2, result.State.CardObjects["P1-BATTLEFIELD-BAR-REGULAR"].Power);
        Assert.Single(result.State.StackItems);
        result = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Equal(3, result.State.CardObjects["P1-BATTLEFIELD-BAR-REGULAR"].Power);
        Assert.Equal(1, result.State.CardObjects["P1-BATTLEFIELD-BAR-REGULAR"].UntilEndOfTurnPowerModifier);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["observerKind"] as string, TriggerKinds.BattlefieldUnitMovedAwayPowerModifier, StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["resultingPower"], 3));
    }

    [Fact]
    public async Task P79BattlefieldMovedUnitPowerSkipsUnrelatedBattlefield()
    {
        var state = BattlefieldMovePowerState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = dirtyObjects["P1-BATTLEFIELD-BACK-ALLEY-BAR"] with
        {
            ControllerId = "P2"
        };

        dirtyObjects["OTHER"] = new("OTHER", cardNo: "OGN·275/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Battlefields = [..state.PlayerZones["P1"].Battlefields, "OTHER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER"), ["P1-BATTLEFIELD-BAR-REGULAR"] = new("P1", "BATTLEFIELD", "OTHER") } };
        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P1-BATTLEFIELD-BAR-REGULAR"] = new("P1", "BATTLEFIELD", "OTHER") } },
            new PlayerIntent("intent-p7-9-battlefield-move-power-dirty-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-BAR-REGULAR",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["P1-BATTLEFIELD-BACK-ALLEY-BAR", "OTHER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-BATTLEFIELD-BAR-REGULAR"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(2, result.State.CardObjects["P1-BATTLEFIELD-BAR-REGULAR"].Power);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-BAR-REGULAR"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(result.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
    }

    [Fact]
    public async Task P79BattlefieldStaticPreventsUnitPlayToBattlefield()
    {
        var state = BattlefieldPreventPlayUnitsState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-static-prevent-play-units", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "OGN·012/298",
                [],
                Destination: "BATTLEFIELD:P1-BATTLEFIELD-FALLING-ROCKS"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("战场效果禁止将单位打出到该战场。", result.ErrorMessage);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-BATTLEFIELD-FALLING-ROCKS", "P1-BATTLEFIELD-FRIENDLY-001"],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79BattlefieldStaticReducesEchoCost()
    {
        var state = BattlefieldEchoCostReductionState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-echo-cost-reduction", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CENTER-STAGE",
                "UNL-061/219",
                [],
                OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(2, stackItem.EffectRepeatCount);
        Assert.Equal(["ECHO"], stackItem.OptionalCosts);
        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costPaid.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costPaid.Payload["paymentWindow"]);
        Assert.Equal("P1-SPELL-CENTER-STAGE", costPaid.Payload["sourceObjectId"]);
        Assert.Equal(3, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Equal(2, costPaid.Payload["baseManaCost"]);
        Assert.Equal(3, costPaid.Payload["totalManaCost"]);
        Assert.Equal(1, costPaid.Payload["battlefieldEchoCostReductionMana"]);
        Assert.Equal(["ECHO"], Assert.IsAssignableFrom<IReadOnlyList<string>>(costPaid.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource()
    {
        var state = BattlefieldEchoCostReductionState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-MARAI-SPIRE"] = dirtyObjects["P1-BATTLEFIELD-MARAI-SPIRE"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-echo-cost-reduction-dirty-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CENTER-STAGE",
                "UNL-061/219",
                [],
                OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CENTER-STAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Fact]
    public void P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource()
    {
        var state = BattlefieldEchoCostReductionState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-MARAI-SPIRE"] = dirtyObjects["P1-BATTLEFIELD-MARAI-SPIRE"] with
        {
            ControllerId = "P2"
        };

        var prompt = ResolutionResult.BuildPrompts(state with { CardObjects = dirtyObjects })["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();

        Assert.DoesNotContain(optionalCostChoices, choice => string.Equals(choice.Id, "ECHO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldStaticReducesFirstEquipmentCost()
    {
        var state = BattlefieldEquipmentCostReductionState();
        state = PrintedCostFixture.Add(state, "P1", "SFD·022/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-equipment-cost-reduction", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                ["P1-UNIT-EQUIPMENT-COST-TARGET"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Single(result.State.StackItems);
        Assert.Contains("PLAYED_EQUIPMENT_THIS_TURN:P1", result.State.UntilEndOfTurnEffects);
        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Equal(1, costPaid.Payload["battlefieldEquipmentCostReductionMana"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticEagerApprenticeReducesSpellCost()
    {
        var state = BattlefieldEagerApprenticeSpellCostReductionState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-eager-apprentice-spell-cost-reduction", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-WELL-TRAINED",
                "OGN·058/298",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Single(result.State.StackItems);
        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Equal(1, costPaid.Payload["battlefieldSpellCostReductionMana"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticEagerApprenticeSkipsOpponentControlledSource()
    {
        var state = BattlefieldEagerApprenticeSpellCostReductionState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-UNIT-EAGER-APPRENTICE"] = dirtyObjects["P1-UNIT-EAGER-APPRENTICE"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-eager-apprentice-spell-cost-dirty-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-WELL-TRAINED",
                "OGN·058/298",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-WELL-TRAINED"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79ArenaServiceCrewReadiesWhenControllerPlaysEquipment()
    {
        var state = ArenaServiceCrewEquipmentReadyState();
        state = PrintedCostFixture.Add(state, "P1", "SFD·022/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-arena-service-crew-equipment-ready", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                ["P1-UNIT-ARENA-SERVICE-CREW"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.False(result.State.CardObjects["P1-UNIT-ARENA-SERVICE-CREW"].IsExhausted);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-ARENA-SERVICE-CREW", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79ArenaServiceCrewSkipsOpponentEquipment()
    {
        var state = ArenaServiceCrewEquipmentReadyState(playerId: "P2");
        state = PrintedCostFixture.Add(state, "P2", "SFD·022/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-arena-service-crew-opponent-equipment", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                ["P2-UNIT-EQUIPMENT-TARGET"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-UNIT-ARENA-SERVICE-CREW"].IsExhausted);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79ArenaServiceCrewSkipsEquipmentTriggerWhenSourceIsStandby()
    {
        var state = AddArenaEquipmentTarget(
            WithFaceDownStandby(ArenaServiceCrewEquipmentReadyState(), "P1-UNIT-ARENA-SERVICE-CREW"));
        state = PrintedCostFixture.Add(state, "P1", "SFD·022/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-arena-service-crew-standby-equipment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                ["P1-UNIT-EQUIPMENT-TARGET"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-UNIT-ARENA-SERVICE-CREW"].IsExhausted);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
    }

    [Fact]
    public void P79BattlefieldStaticEagerApprenticePromptShowsSpellCostReduction()
    {
        var state = BattlefieldEagerApprenticeSpellCostReductionState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-SPELL-WELL-TRAINED", sourceRequirement["sourceObjectId"]);
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minimumManaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["battlefieldSpellCostReductionMana"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource()
    {
        var state = BattlefieldEquipmentCostReductionState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-ORNN-FORGE"] = dirtyObjects["P1-BATTLEFIELD-ORNN-FORGE"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-equipment-cost-reduction-dirty-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                ["P1-UNIT-EQUIPMENT-COST-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Fact]
    public void P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource()
    {
        var state = BattlefieldEquipmentCostReductionState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-ORNN-FORGE"] = dirtyObjects["P1-BATTLEFIELD-ORNN-FORGE"] with
        {
            ControllerId = "P2"
        };

        var prompt = ResolutionResult.BuildPrompts(state with { CardObjects = dirtyObjects })["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
    }

    [Fact]
    public async Task P79BattlefieldFriendlySpellTargetDrawsOnce()
    {
        var state = BattlefieldFriendlySpellDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-friendly-spell-draw", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P1-BATTLEFIELD-ALLY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await SpellResolutionTestDriver.Top(result);
        Assert.Equal(["P1-MAIN-DRAWN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal([], result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(
            result.State.UntilEndOfTurnEffects,
            effectId => string.Equals(
                effectId,
                "BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:P1:P1-BATTLEFIELD-DREAMTREE",
                StringComparison.Ordinal));
        var trigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-DREAMTREE", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal(1, trigger.Payload["drawCount"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["cardNo"] as string, "SFD·034/221", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFriendlySpellTargetDoesNotDrawTwiceThisTurn()
    {
        var state = BattlefieldFriendlySpellDrawState() with
        {
            UntilEndOfTurnEffects = ["BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:P1:P1-BATTLEFIELD-DREAMTREE"]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-friendly-spell-draw-second", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P1-BATTLEFIELD-ALLY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MAIN-DRAWN"], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFriendlySpellTargetSkipsDifferentBattlefieldUnit()
    {
        var state = BattlefieldFriendlySpellDrawState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Battlefields = ["P1-BATTLEFIELD-DREAMTREE", "P1-BATTLEFIELD-OTHER", "P1-BATTLEFIELD-ALLY"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-OTHER"] = new(
            "P1-BATTLEFIELD-OTHER",
            cardNo: "OGN·276/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P1",
            controllerId: "P1");
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-SAVAGE-STRENGTH"] = new("P1", "HAND"),
            ["P1-MAIN-DRAWN"] = new("P1", "MAIN_DECK"),
            ["P1-BATTLEFIELD-DREAMTREE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-DREAMTREE"),
            ["P1-BATTLEFIELD-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER"),
            ["P1-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER")
        };
        state = state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-friendly-spell-draw-different-battlefield", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P1-BATTLEFIELD-ALLY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MAIN-DRAWN"], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(
            result.State.UntilEndOfTurnEffects,
            effectId => string.Equals(
                effectId,
                "BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:P1:P1-BATTLEFIELD-DREAMTREE",
                StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }


    [Fact]
    public async Task P79BattlefieldSpellPowerBonusBuffsControlledUnitOnSpellPlay()
    {
        var state = BattlefieldSpellPowerBonusState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-spell-power-bonus", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P1-BATTLEFIELD-ALLY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await SpellResolutionTestDriver.Finish(result);
        var target = result.State.CardObjects["P1-BATTLEFIELD-ALLY"];
        Assert.Equal(5, target.Power);
        Assert.Equal(3, target.UntilEndOfTurnPowerModifier);
        var trigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_SPELL_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-WASTE-HALL", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-ALLY", trigger.Payload["targetObjectId"]);
        var powerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal) && Equals(gameEvent.Payload["powerDelta"], 1));
        Assert.Equal(1, powerEvent.Payload["appliedPowerDelta"]);
        Assert.Equal(5, powerEvent.Payload["resultingPower"]);
    }

    [Fact]
    public async Task P79BattlefieldSpellPowerBonusSkipsDifferentBattlefieldUnit()
    {
        var state = BattlefieldSpellPowerBonusState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Battlefields = ["P1-BATTLEFIELD-WASTE-HALL", "P1-BATTLEFIELD-OTHER", "P1-BATTLEFIELD-ALLY"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-OTHER"] = new(
            "P1-BATTLEFIELD-OTHER",
            cardNo: "OGN·276/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P1",
            controllerId: "P1");
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-SAVAGE-STRENGTH"] = new("P1", "HAND"),
            ["P1-BATTLEFIELD-WASTE-HALL"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-WASTE-HALL"),
            ["P1-BATTLEFIELD-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER"),
            ["P1-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER")
        };
        state = state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-spell-power-bonus-different-battlefield", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P1-BATTLEFIELD-ALLY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var target = result.State.CardObjects["P1-BATTLEFIELD-ALLY"];
        Assert.Equal(2, target.Power);
        Assert.Equal(0, target.UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_SPELL_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
    }


    [Fact]
    public async Task P79BattlefieldHighCostSpellInsightOffersPrivateChoiceAfterResponse()
    {
        var state = BattlefieldHighCostSpellInsightState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-high-cost-spell-insight", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MOONFALL",
                "UNL-066/219",
                ["P2-BATTLEFIELD-ENEMY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(state.PlayerZones["P1"].MainDeck, result.State.PlayerZones["P1"].MainDeck);
        Assert.Null(result.State.StackItems.Last().InsightContext);
        result = await SpellResolutionTestDriver.Top(result);
        Assert.NotNull(result.State.StackItems.Last().InsightContext);
        Assert.DoesNotContain(result.Events, e => e.Kind == "CARDS_RECYCLED");
        var opened = await OfficialInsightTriggerTests.ResolveTop(result.State);
        Assert.Equal("INSIGHT", opened.State.PendingCardChoice!.ChoiceWindow);
        var completed = await OfficialInsightTriggerTests.Choose(opened.State, ["P1-INSIGHT-RECYCLE"]);
        Assert.Equal(["P1-MAIN-KEEPER", "P1-INSIGHT-RECYCLE"], completed.State.PlayerZones["P1"].MainDeck);
        var recycleEvent = Assert.Single(completed.Events, e => e.Kind == "CARDS_RECYCLED");
        Assert.Equal("P1-BATTLEFIELD-LOST-LIBRARY", recycleEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, recycleEvent.Payload["count"]);
        Assert.DoesNotContain(completed.Events, e => e.Payload.ContainsKey("cardIds") || e.Payload.ContainsKey("recycledCardIds"));

    }

    [Fact]
    public async Task P79BattlefieldHighCostSpellInsightSkipsLowCostSpell()
    {
        var state = BattlefieldHighCostSpellInsightState(
            mana: 2,
            sourceObjectId: "P1-SPELL-SAVAGE-STRENGTH",
            cardNo: "SFD·034/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-high-cost-spell-insight-low-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SAVAGE-STRENGTH",
                "SFD·034/221",
                ["P2-BATTLEFIELD-ENEMY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-INSIGHT-RECYCLE", "P1-MAIN-KEEPER"], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["effectKind"], "INSIGHT_TRIGGER"));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHighCostSpellInsightSkipsOpponentControlledSource()
    {
        var state = BattlefieldHighCostSpellInsightState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-LOST-LIBRARY"] = dirtyObjects["P1-BATTLEFIELD-LOST-LIBRARY"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-high-cost-spell-insight-dirty-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MOONFALL",
                "UNL-066/219",
                ["P2-BATTLEFIELD-ENEMY"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-INSIGHT-RECYCLE", "P1-MAIN-KEEPER"], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["effectKind"], "INSIGHT_TRIGGER"));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience()
    {
        var state = BattlefieldUnitExperienceAbilityState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-ability", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P1-BATTLEFIELD-MUTATION-GARDEN"].Zone);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P1-BATTLEFIELD-EXPERIENCE-UNIT"].Zone);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        var trigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-MUTATION-GARDEN", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal(1, trigger.Payload["amount"]);
        var experienceEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, experienceEvent.Payload["amount"]);
        Assert.Equal(1, experienceEvent.Payload["totalExperience"]);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsOutsideOpenMainWindowWithChineseError()
    {
        var state = BattlefieldUnitExperienceAbilityState() with
        {
            TimingState = TimingStates.NeutralClosed
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-timing", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("战场授予经验技能只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Battlefield unit", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsTargetSelectionWithChineseError()
    {
        var state = BattlefieldUnitExperienceAbilityState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-target", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                ["P1-BATTLEFIELD-MUTATION-GARDEN"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("战场授予经验技能不接受目标或额外费用。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Battlefield unit", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsNonBattlefieldSourceWithChineseError()
    {
        var state = BattlefieldUnitExperienceAbilityState() with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BATTLEFIELD-EXPERIENCE-UNIT"],
                    Battlefields = ["P1-BATTLEFIELD-MUTATION-GARDEN"]
                },
                ["P2"] = PlayerZones.Empty
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-source-zone", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("战场授予经验技能需要选择当前玩家控制的战场单位。", result.ErrorMessage);
        Assert.DoesNotContain("Battlefield unit", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }
}
